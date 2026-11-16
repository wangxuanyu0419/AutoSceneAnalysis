import json
import struct
import tempfile
import unittest
import importlib.util
from pathlib import Path

import numpy as np
from PIL import Image

if importlib.util.find_spec("cv2") is None or importlib.util.find_spec("scipy") is None:
    raise unittest.SkipTest("Install the reconstruction extra to run geometry tests")

from autoscene.reconstruction import floor_frame, triangulate, export_scene, reviewed_observations


class ReconstructionGeometryTests(unittest.TestCase):
    def test_same_size_different_photo_cannot_reuse_reviewed_outlines(self):
        record={"image_size_px":[30,20],"image_rgb_sha256":"another_image","observations":[]}
        with self.assertRaises(ValueError):reviewed_observations(Image.new("RGB",(30,20)),record)

    def test_camera_axes_and_floor_do_not_mirror_the_room(self):
        h,w=30,40
        xx,yy=np.meshgrid(np.linspace(-2,2,w),np.linspace(.3,1.5,h))
        points=np.stack([xx,np.ones_like(xx)*1.5,8-yy],-1).astype("float32")
        rot,offset,fit=floor_frame(points,np.ones((h,w),bool))
        world=(points*[1,-1,1])@rot.T+offset
        self.assertTrue(fit["accepted"])
        self.assertLess(np.abs(world[...,1]).max(),1e-5)
        self.assertGreater(world[15,-1,0],world[15,0,0])

    def test_triangles_cannot_bridge_depth_edges_or_instance_boundaries(self):
        yy,xx=np.indices((8,8)); p=np.stack([xx*.01,yy*.01,np.where(xx<4,2.,8.)],-1)
        labels=(xx>=4).astype(int)
        _,_,ids,tri=triangulate(p,np.ones((8,8),bool),labels,step=1)
        self.assertTrue(len(tri)>0)
        self.assertTrue((ids[tri]==ids[tri[:,:1]]).all())
        self.assertFalse(any(set(xx.ravel()[t]) & {3} and set(xx.ravel()[t]) & {4} for t in tri))

    def test_export_keeps_identity_geometry_actions_and_intention_separate(self):
        yy,xx=np.indices((20,30)); p=np.stack([(xx-15)*.01,(yy-10)*.01,np.full_like(xx,3,dtype=float)],-1)
        maps={"points":p,"depth":p[...,2],"mask":np.ones((20,30),bool),"intrinsics":np.eye(3)}
        with tempfile.TemporaryDirectory() as folder:
            scene=export_scene(Image.new("RGB",(30,20)),maps,[{"id":"tv","label":"television","polygon_px":[[2,2],[12,2],[12,12],[2,12]]}],folder,{},step=1)
            target=next(o for o in scene["objects"] if o["id"]=="tv")
            self.assertIsNone(target["decoded_intention"])
            self.assertEqual(target["action_feasibility"],"unknown")
            self.assertEqual(target["geometry"]["hidden_surfaces"],"not recovered")
            with (Path(folder)/"surfaces.bin").open("rb") as f:
                self.assertEqual(f.read(4),b"ASR1")
                self.assertEqual(struct.unpack("<i",f.read(4))[0],2)


if __name__=="__main__":unittest.main()
