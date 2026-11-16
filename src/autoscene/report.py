"""Portable visual artifacts that open directly from disk."""
import json

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def color(index):
    return [(54, 216, 187), (252, 187, 88), (115, 164, 255), (248, 134, 167)][index % 4]


def font(size):
    for name in ["C:/Windows/Fonts/arial.ttf", "DejaVuSans.ttf"]:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def save_artifacts(image, depth, scene, metrics, output):
    image.save(output / "input.png")
    overlay = image.copy()
    draw = ImageDraw.Draw(overlay)
    label_font = font(max(12, round(image.width / 55)))
    for i, obj in enumerate(scene["objects"]):
        box = obj["bbox_xyxy_px"]
        draw.rectangle(box, outline=color(i), width=3)
        label = f'{obj["id"][4:]} {obj["label"]}'
        bounds = draw.textbbox((0, 0), label, font=label_font)
        tx = max(0, min(box[0], image.width - (bounds[2] - bounds[0]) - 8))
        ty = max(0, box[1] - (bounds[3] - bounds[1]) - 12)
        draw.rectangle([tx, ty, tx + bounds[2] - bounds[0] + 8, ty + bounds[3] - bounds[1] + 10], fill=color(i))
        draw.text((tx + 4, ty + 3), label, fill=(12, 23, 34), font=label_font)
    overlay.save(output / "objects.png")
    finite = depth[np.isfinite(depth)]
    if finite.size:
        lo, hi = np.percentile(finite, [2, 98])
        normalized = np.clip((np.nan_to_num(depth, nan=float(lo), posinf=float(hi), neginf=float(lo)) - lo) / max(float(hi - lo), 1e-8), 0, 1)
    else:
        normalized = np.zeros_like(depth)
    # A fixed palette encodes near as warm and far as dark blue.
    rgb = np.stack([normalized * 245 + 10, normalized ** .7 * 185 + 25, (1 - normalized) * 140 + 50], axis=-1).clip(0, 255).astype("uint8")
    depth_image = Image.fromarray(rgb)
    depth_image.save(output / "depth.png")
    np.save(output / "relative_inverse_depth.npy", depth)
    preview = Image.new("RGB", (image.width * 2, image.height + 60), (15, 23, 38))
    preview.paste(overlay, (0, 60))
    preview.paste(depth_image, (image.width, 60))
    pd = ImageDraw.Draw(preview)
    pd.text((16, 16), "Object candidates", font=font(22), fill="white")
    pd.text((image.width + 16, 16), "Relative depth | warm = nearer | no metre scale", font=font(18), fill="white")
    preview.save(output / "preview.png")
    (output / "scene.json").write_text(json.dumps(scene, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    (output / "metrics.json").write_text(json.dumps(metrics, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    html = ("""<!doctype html><html lang="en"><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Room object and action demo</title>
<style>
:root{color-scheme:dark;font:16px/1.6 system-ui,sans-serif;background:#101825;color:#eef2fa}*{box-sizing:border-box}
body{max-width:1380px;margin:auto;padding:32px}h1{font-size:34px;line-height:1.15;margin:8px 0 16px}h2{font-size:20px}
p{color:#b8c5d8;max-width:80ch}.eyebrow{color:#56dbc3;font-size:13px;letter-spacing:.13em;text-transform:uppercase}
.layout{display:grid;grid-template-columns:minmax(0,1.65fr) minmax(300px,1fr);gap:24px}.card{background:#172236;border:1px solid #2a3a52;border-radius:14px;padding:20px;margin:16px 0}
#objects{max-height:245px;overflow:auto;padding:2px}
img{display:block;width:100%;border-radius:8px}.buttons{display:flex;flex-wrap:wrap;gap:8px}button{border:1px solid #455572;background:#22334d;color:#edf3ff;padding:8px 12px;border-radius:7px;cursor:pointer;font:inherit}
button.active{border-color:#56dbc3;background:#19443f}button:focus-visible{outline:3px solid #f4c67c}button:hover{background:#2d435f}
pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#0d1522;padding:16px;border-radius:8px;font-size:13px}small{color:#a5b7cd}.stat{font-size:26px;color:#56dbc3}a{color:#8ac1ff}.tag{display:inline-block;color:#f4c67c;border:1px solid #765e38;border-radius:5px;padding:2px 8px;font-size:12px}
@media(max-width:880px){body{padding:16px}.layout{grid-template-columns:1fr}h1{font-size:28px}}
</style>
<div class="eyebrow">AutoSceneAnalysis / local prototype</div><h1>A room as objects and actions</h1>
<p>Select a target, then a suggested action. The output shows the two vectors a future neural decoder could supply. These controls use the mouse; no neural signal or robot is connected.</p>
<div class="tag" id="mode"></div>
<div class="layout"><section>
<div class="card"><h2>Visible object candidates</h2><img src="objects.png" alt="Image annotated with object labels and boxes"><p id="count"></p></div>
<div class="card"><h2>Relative depth</h2><img src="depth.png" alt="Relative inverse depth"><small>Warm colours mean nearer surfaces. This has no metre scale. Depth under a box can include other objects.</small></div>
</section><section>
<div class="card"><div class="eyebrow">Axis 1</div><h2>Choose an object</h2><div class="buttons" id="objects"></div><p id="details"></p></div>
<div class="card"><div class="eyebrow">Axis 2</div><h2>Choose an interaction</h2><div class="buttons" id="actions"></div><p>Actions come from a small hand-written category list. Their physical feasibility is unknown.</p><div class="buttons"><button id="cancel">Idle / cancel</button></div></div>
<div class="card"><h2>Decoder interface preview</h2><pre id="selection">Choose an object and an action.</pre><button id="download">Save simulated selection</button></div>
<div class="card"><h2>Measured perception time</h2><div class="stat" id="timing"></div><p id="timing-note"></p><small>Still image processing; excludes camera capture, display, and neural decoding.</small></div>
</section></div>
<script id="scene-data" type="application/json">__SCENE__</script>
<script id="metric-data" type="application/json">__METRICS__</script>
<script>
const scene=JSON.parse(document.getElementById('scene-data').textContent), metrics=JSON.parse(document.getElementById('metric-data').textContent);
const iface=scene.decoder_interface;let selected=null,selection=null;
const byId=id=>document.getElementById(id);
byId('mode').textContent=metrics.mode==='fixture'?'Synthetic interface example — models were not run':'Model inference — category-based action suggestions';
byId('count').textContent=scene.objects.length+' candidates. IDs identify this snapshot only. Objects outside the prompt list may be missed.';
byId('timing').textContent=metrics.mode==='fixture'?'Not measured':metrics.perception_ms.p50.toFixed(0)+' ms median';
byId('timing-note').textContent=metrics.mode==='fixture'?'This example tests the interface.':metrics.device_name+' · '+metrics.repeats+' repeated runs · p95 '+metrics.perception_ms.p95.toFixed(0)+' ms. Small repeat counts give only a rough timing check.';
function clear(){selection=null;byId('selection').textContent=JSON.stringify({control:'idle',input_source:'mouse_simulation'},null,2)}
function chooseObject(obj){selected=obj;selection=null;document.querySelectorAll('#objects button').forEach(b=>b.classList.toggle('active',b.dataset.id===obj.id));
byId('details').textContent=obj.id+' · detector score '+obj.detector_score.toFixed(2)+' (not a calibrated probability) · 3D metric position unavailable.';
byId('actions').replaceChildren();obj.action_candidates.forEach(candidate=>{const b=document.createElement('button');b.textContent=candidate.action.replaceAll('_',' ');b.onclick=()=>{
document.querySelectorAll('#actions button').forEach(x=>x.classList.remove('active'));b.classList.add('active');
selection={frame_id:scene.frame_id,object_id:obj.id,action:candidate.action,object_one_hot:iface.object_ids.map(x=>Number(x===obj.id)),action_one_hot:iface.action_vocabulary.map(x=>Number(x===candidate.action)),input_source:'mouse_simulation',executes_robot_motion:false};
byId('selection').textContent=JSON.stringify(selection,null,2)};byId('actions').append(b)});byId('selection').textContent='Now choose an interaction.'}
scene.objects.forEach(obj=>{const b=document.createElement('button');b.textContent=obj.id+' '+obj.label;b.dataset.id=obj.id;b.onclick=()=>chooseObject(obj);byId('objects').append(b)});
if(!scene.objects.length)byId('objects').textContent='No candidates above the threshold. Try a clearer image or edit the prompt list.';
byId('cancel').onclick=()=>{selected=null;document.querySelectorAll('button.active').forEach(b=>b.classList.remove('active'));byId('actions').replaceChildren();clear()};
byId('download').onclick=()=>{if(!selection)return;const u=URL.createObjectURL(new Blob([JSON.stringify(selection,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=u;a.download='simulated-selection.json';a.click();URL.revokeObjectURL(u)};
</script></html>""")
    html = html.replace("__SCENE__", json.dumps(scene, allow_nan=False).replace("</", "<\\/"))
    html = html.replace("__METRICS__", json.dumps(metrics, allow_nan=False).replace("</", "<\\/"))
    (output / "report.html").write_text(html, encoding="utf-8")
