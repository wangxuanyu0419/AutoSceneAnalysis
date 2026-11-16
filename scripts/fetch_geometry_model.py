"""Fetch an official MoGe checkpoint in bounded chunks and verify its published hash."""
import argparse
import hashlib
import json
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

import requests
from huggingface_hub import HfApi

ROOT=Path(__file__).resolve().parents[1]


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model",default="Ruicheng/moge-2-vitb-normal")
    parser.add_argument("--workers",type=int,default=8)
    parser.add_argument("--revision")
    args=parser.parse_args()
    revision=args.revision or ("ca5f0e07ff01d3e5a364c1d954ed12ee1814b368" if args.model=="Ruicheng/moge-2-vitb-normal" else None)
    info=HfApi().model_info(args.model,revision=revision,files_metadata=True)
    record=next(f for f in info.siblings if f.rfilename=="model.pt")
    destination=ROOT/"models"/(args.model.split("/")[-1]+".pt")
    chunks=destination.parent/(destination.stem+".chunks");chunks.mkdir(parents=True,exist_ok=True)
    chunk_size=2*1024*1024
    total=(record.size+chunk_size-1)//chunk_size
    def fetch(index):
        start=index*chunk_size;end=min(record.size,start+chunk_size)-1
        path=chunks/f"{index:04d}.bin"
        if path.exists() and path.stat().st_size==end-start+1:return path
        for attempt in range(3):
            try:
                url=f"https://huggingface.co/{args.model}/resolve/{info.sha}/model.pt?download=true&chunk={index}&attempt={attempt}"
                with requests.get(url,headers={"Range":f"bytes={start}-{end}"},stream=True,timeout=(15,35)) as response:
                    if response.status_code!=206:raise RuntimeError(f"Host did not honor byte range: {response.status_code}")
                    expected=f"bytes {start}-{end}/{record.size}"
                    if response.headers.get("Content-Range")!=expected:raise RuntimeError("Incorrect byte range")
                    with path.with_suffix(".partial").open("wb") as out:
                        for part in response.iter_content(65536):out.write(part)
                if path.with_suffix(".partial").stat().st_size!=end-start+1:raise RuntimeError("Incomplete chunk")
                path.with_suffix(".partial").replace(path)
                return path
            except (requests.RequestException,RuntimeError):
                if attempt==2:raise
                time.sleep(1)
    completed=0
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for future in as_completed([pool.submit(fetch,i) for i in range(total)]):
            future.result();completed+=1
            if completed%10==0 or completed==total:print(f"Verified range lengths: {completed}/{total}",flush=True)
    digest=hashlib.sha256()
    temporary=destination.with_suffix(".assembled")
    with temporary.open("wb") as out:
        for index in range(total):
            data=(chunks/f"{index:04d}.bin").read_bytes();digest.update(data);out.write(data)
    if not record.lfs or digest.hexdigest()!=record.lfs.sha256:raise RuntimeError("Checkpoint SHA256 mismatch")
    temporary.replace(destination)
    metadata={"model":args.model,"revision":info.sha,"sha256":digest.hexdigest(),"bytes":record.size}
    destination.with_suffix(".json").write_text(json.dumps(metadata,indent=2)+"\n")
    print(json.dumps(metadata,indent=2),flush=True)


if __name__=="__main__":main()
