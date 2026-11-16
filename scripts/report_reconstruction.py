"""Create a local side-by-side round-trip report from completed benchmark results."""
import argparse
import html
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dataset",type=Path,required=True)
    args=parser.parse_args()
    result=json.loads((args.dataset/"benchmark-results.json").read_text())
    font=ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf",17)
    sheet=Image.new("RGB",(1280,4*250),(244,245,247));draw=ImageDraw.Draw(sheet)
    cards=[]
    for index,score in enumerate(result["scenes"]):
        folder="scene-"+str(score["seed"])
        paths=[folder+"/rgb.png",folder+"/prediction/unity-reference-view.png"]
        for side,path in enumerate(paths):
            picture=Image.open(args.dataset/path).convert("RGB").resize((320,213))
            x=(index%2)*640+side*320;y=(index//2)*250
            sheet.paste(picture,(x,y+32))
            draw.text((x+6,y+5),f'{score["seed"]} · '+("input" if side==0 else "reconstruction"),font=font,fill=(25,35,45))
        cards.append(f'<article><h2>{score["seed"]} · {html.escape(score["split"])}</h2>'
            +''.join(f'<figure><img src="{p}"><figcaption>{label}</figcaption></figure>' for p,label in zip(paths,["RGB input","Unity reconstruction, source camera"]))
            +f'<p>Depth error: {score["metric_abs_rel"]:.1%}; RMSE: {score["metric_rmse_m"]:.2f} m. Target recall: {score["target_recall_iou_025"]:.0%}.</p></article>')
    sheet.save(args.dataset/"comparison-grid.png")
    held=result["summaries"]["held_out"]
    page=f'''<!doctype html><html lang="en"><meta charset="utf-8"><title>Room reconstruction round trip</title>
<style>body{{font:17px system-ui;max-width:1360px;margin:32px auto;padding:0 24px;color:#172638;background:#f4f6f8}}h1{{font-size:32px}}article{{padding:20px;background:white;margin:22px 0;border-radius:12px}}figure{{display:inline-block;margin:0 1% 0 0;width:48%}}img{{width:100%}}figcaption{{font-size:14px;color:#526171}}p{{line-height:1.6}}</style>
<h1>Generate → photograph → reconstruct → score</h1>
<p>Eight varied procedural rooms; four development and four held-out seeds. Reconstruction receives RGB and camera calibration. Simulator truth is read only by the evaluator.</p>
<p><strong>Held-out visible-depth error: {held["metric_abs_rel"]:.1%}</strong>, versus {held["constant_5m_abs_rel_baseline"]:.1%} for a constant 5 m depth. Source-view image similarity reuses the input texture and does not establish 3D accuracy.</p>
{''.join(cards)}<p>These simple rooms test placement and depth. Complete hidden geometry, realistic room clutter, articulation and neural control remain separate questions.</p></html>'''
    (args.dataset/"report.html").write_text(page,encoding="utf-8")
    print(str((args.dataset/"report.html").resolve()))


if __name__=="__main__":main()
