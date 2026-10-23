"""
Sliders for the people: hair size and position (for every style, and per style), head size, arm size and
drop for men and women, with a live preview of whole figures. "Save" writes people_fit.json and re-cuts all
the parts into the game (then click into Unity so it picks them up).
Run: python3 fit_people.py   and open http://localhost:8765
"""
import io
import json
import os
import sys
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

from PIL import Image
import cut_people as cp

PORT = 8765
LEGS_ROW = {"men": lambda i: 1 if i in (3, 4, 15) else 2 if i == 9 else 0, "women": lambda i: 3}
HIPS = 226   # where the game hangs the legs under the torso (CharacterArt.HipsOnTorso)
lock = threading.Lock()


def figure(group, body, head, hair, fit, ref):
    """One whole person, stacked as the game stacks them: legs, arms, torso, head, hair."""
    c = Image.new("RGBA", (256, 320), (0, 0, 0, 0))
    legs = os.path.join(cp.OUT, f"legs_{LEGS_ROW[group](body)}1.png")
    if os.path.exists(legs):
        c.alpha_composite(Image.open(legs).convert("RGBA"), (0, HIPS - cp.LEGS_TOP))
    parts = cp.torso_and_arms(group, body, fit)
    if parts:
        torso, al, ar = parts[:3]
        for p in (al, ar, torso):
            c.alpha_composite(p)
    h = cp.head_image(head, fit)
    if h:
        c.alpha_composite(h)
    hr = cp.hair_image(group, hair, fit, ref)
    if hr:
        c.alpha_composite(hr)
    return c


def preview(fit, group, style):
    """Top: the chosen style on four people, big. Below: every style of the group, small."""
    ref = cp.head_scale_ref()
    heads = list(range(8)) if group == "men" else list(range(8, 16))
    big = [figure(group, (style + k * 3) % 16, heads[(style + k * 2) % 8], style, fit, ref) for k in range(4)]
    small = [figure(group, i, heads[i % 8], i, fit, ref) for i in range(16)]
    W = 4 * 250
    sheet = Image.new("RGBA", (W, 330 + 2 * 160), (96, 92, 86, 255))
    for k, f in enumerate(big):
        sheet.alpha_composite(f.resize((240, 300), Image.LANCZOS), (k * 250 + 5, 10))
    for k, f in enumerate(small):
        sheet.alpha_composite(f.resize((120, 150), Image.LANCZOS), ((k % 8) * 125 + 2, 330 + (k // 8) * 160))
    buf = io.BytesIO()
    sheet.convert("RGB").save(buf, "JPEG", quality=88)
    return buf.getvalue()


PAGE = """<!doctype html><html><head><meta charset=utf-8><title>People fit</title>
<style>
body{background:#14110d;color:#eadfc8;font:14px -apple-system,Helvetica,sans-serif;margin:0;display:flex}
#side{width:330px;padding:16px;box-sizing:border-box;border-right:1px solid #5a4828;height:100vh;overflow:auto}
h1{font-size:20px;color:#e3b862;letter-spacing:2px;margin:0 0 10px}
h2{font-size:13px;color:#e3b862;letter-spacing:1.5px;margin:18px 0 6px;border-bottom:1px solid #3a2f1c;padding-bottom:3px}
label{display:block;margin:8px 0 2px;color:#b9a57a}
input[type=range]{width:100%}
.v{float:right;color:#eadfc8}
select,button{background:#211a12;color:#eadfc8;border:1px solid #8a6d36;border-radius:4px;padding:6px 10px;font-size:14px}
button{cursor:pointer;margin:12px 6px 0 0}
button.primary{background:#8a6d36;color:#14110d;font-weight:bold}
#status{margin-top:10px;color:#8fd18a;min-height:18px}
#main{flex:1;padding:16px;overflow:auto}
img{max-width:100%;border:1px solid #3a2f1c}
.row{display:flex;gap:8px}
</style></head><body>
<div id=side>
<h1>PEOPLE FIT</h1>
<div class=row><select id=group><option value=men>Men</option><option value=women>Women</option></select>
<select id=style></select></div>

<h2>ALL HAIR (this group's sheet and the other)</h2>
<label>Hair size <span class=v id=v_hsize></span></label><input type=range id=hsize min=0.75 max=1.25 step=0.01>
<label>Hair up / down <span class=v id=v_hup></span></label><input type=range id=hup min=-20 max=20 step=1>

<h2>THIS HAIRSTYLE ONLY</h2>
<label>Size <span class=v id=v_ssize></span></label><input type=range id=ssize min=0.7 max=1.3 step=0.01>
<label>Left / right <span class=v id=v_sx></span></label><input type=range id=sx min=-25 max=25 step=1>
<label>Up / down <span class=v id=v_sy></span></label><input type=range id=sy min=-25 max=25 step=1>
<label><input type=checkbox id=snone> No hair for this style (bald)</label>

<h2>HEADS (everyone)</h2>
<label>Head size <span class=v id=v_head></span></label><input type=range id=head min=0.8 max=1.2 step=0.01>

<h2>ARMS (this group)</h2>
<label>Arm size <span class=v id=v_asize></span></label><input type=range id=asize min=0.6 max=1.4 step=0.01>
<label>Arm up / down <span class=v id=v_adown></span></label><input type=range id=adown min=-25 max=25 step=1>

<button class=primary id=save>Save and re-cut</button><button id=reset>Reset this style</button>
<div id=status></div>
<p style="color:#9c9180;font-size:12px">After saving, click into Unity so it re-imports, then press Play.</p>
</div>
<div id=main><img id=prev></div>
<script>
let fit = %FIT%;
const $ = id => document.getElementById(id);
for (let i = 0; i < 16; i++) { const o = document.createElement('option'); o.value = i; o.textContent = 'Style ' + (i + 1); $('style').appendChild(o); }
function own() { const g = $('group').value, s = $('style').value; fit.hair[g] = fit.hair[g] || {}; let o = fit.hair[g][s]; return o; }
function show() {
  const g = $('group').value, s = $('style').value, o = fit.hair[g] && fit.hair[g][s];
  $('hsize').value = fit.hair.size; $('hup').value = fit.hair.up; $('head').value = fit.heads.size;
  const a = fit.arms[g] || {size: 1, down: 0}; $('asize').value = a.size; $('adown').value = a.down;
  $('snone').checked = o === 'none';
  const oo = (o && o !== 'none') ? o : {};
  $('ssize').value = oo.size ?? 1; $('sx').value = oo.x ?? 0; $('sy').value = oo.y ?? 0;
  labels(); refresh();
}
function labels() { for (const id of ['hsize','hup','ssize','sx','sy','head','asize','adown']) $('v_' + id).textContent = $(id).value; }
function read() {
  const g = $('group').value, s = $('style').value;
  fit.hair.size = +$('hsize').value; fit.hair.up = +$('hup').value; fit.heads.size = +$('head').value;
  fit.arms[g] = {size: +$('asize').value, down: +$('adown').value};
  fit.hair[g] = fit.hair[g] || {};
  if ($('snone').checked) fit.hair[g][s] = 'none';
  else {
    const o = {size: +$('ssize').value, x: +$('sx').value, y: +$('sy').value};
    if (o.size === 1 && o.x === 0 && o.y === 0) delete fit.hair[g][s]; else fit.hair[g][s] = o;
  }
  labels();
}
let timer = null;
function refresh() {
  clearTimeout(timer);
  timer = setTimeout(() => {
    $('prev').src = '/preview?g=' + $('group').value + '&s=' + $('style').value + '&fit=' + encodeURIComponent(JSON.stringify(fit)) + '&t=' + Date.now();
  }, 200);
}
for (const id of ['hsize','hup','ssize','sx','sy','head','asize','adown','snone']) $(id).addEventListener('input', () => { read(); refresh(); });
$('group').addEventListener('change', show); $('style').addEventListener('change', show);
$('reset').onclick = () => { const g = $('group').value; delete fit.hair[g][$('style').value]; show(); };
$('save').onclick = async () => {
  $('status').textContent = 'Saving and cutting...';
  const r = await fetch('/save', {method: 'POST', body: JSON.stringify(fit)});
  $('status').textContent = await r.text();
};
show();
</script></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send(self, code, body, kind):
        self.send_response(code)
        self.send_header("Content-Type", kind)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        u = urlparse(self.path)
        if u.path == "/":
            page = PAGE.replace("%FIT%", json.dumps(cp.load_fit()))
            return self.send(200, page.encode(), "text/html; charset=utf-8")
        if u.path == "/preview":
            q = parse_qs(u.query)
            fit = json.loads(q["fit"][0])
            with lock:
                img = preview(fit, q.get("g", ["men"])[0], int(q.get("s", ["0"])[0]))
            return self.send(200, img, "image/jpeg")
        self.send(404, b"", "text/plain")

    def do_POST(self):
        if urlparse(self.path).path != "/save":
            return self.send(404, b"", "text/plain")
        fit = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        with lock:
            with open(cp.FIT_PATH, "w") as f:
                json.dump(fit, f, indent=2)
            cp.build_all(fit)
        self.send(200, "Saved. Click into Unity, then press Play.".encode(), "text/plain; charset=utf-8")


if __name__ == "__main__":
    print(f"People fit: http://localhost:{PORT}  (Ctrl+C to stop)")
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    if "--no-browser" not in sys.argv:
        threading.Timer(0.8, lambda: webbrowser.open(f"http://localhost:{PORT}")).start()
    server.serve_forever()
