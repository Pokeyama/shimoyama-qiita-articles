"""Build tech-stack.drawio; export the PNG with draw.io Desktop."""
from pathlib import Path
import base64
import xml.etree.ElementTree as ET

BASE = Path(__file__).resolve().parent
FONT = 'fontFamily=Hiragino Sans;fontColor=#243247;fontSize=18;whiteSpace=wrap;html=0;'
W, H = 780, 650

doc = ET.Element('mxfile', host='app.diagrams.net', type='device')
page = ET.SubElement(doc, 'diagram', id='tech-stack', name='tech-stack')
model = ET.SubElement(page, 'mxGraphModel', grid='1', gridSize='10', page='1', pageScale='1',
                      pageWidth=str(W), pageHeight=str(H), background='#ffffff')
root = ET.SubElement(model, 'root')
ET.SubElement(root, 'mxCell', id='0')
ET.SubElement(root, 'mxCell', id='1', parent='0')


def cell(key, value, x, y, w, h, style):
    c = ET.SubElement(root, 'mxCell', id=key, value=value, vertex='1', parent='1', style=FONT + style)
    ET.SubElement(c, 'mxGeometry', x=str(x), y=str(y), width=str(w), height=str(h), **{'as': 'geometry'})


def text(key, value, x, y, w, h=28, size=18, bold=False, color='#243247'):
    cell(key, value, x, y, w, h, f'fillColor=none;strokeColor=none;fontSize={size};fontColor={color};'
                                 f'fontStyle={1 if bold else 0};align=center;verticalAlign=middle;')


def image(key, name, x, y, w, h):
    path = BASE / 'icons' / name
    mime = 'svg+xml' if path.suffix == '.svg' else 'png'
    data = base64.b64encode(path.read_bytes()).decode()
    cell(key, '', x, y, w, h, f'shape=image;imageAspect=1;image=data:image/{mime},{data};fillColor=none;strokeColor=none;')


def logo(key, name, label, cx, size, w=None):
    """Square-ish icon centered on cx with its name underneath."""
    w = w or size
    top = 30 + ROW * 150
    image(key, name, cx - w / 2, top + 52 - size / 2, w, size)
    if label:
        text(key + '-label', label, cx - 90, top + 94, 180, 26, 17)


for i, title in enumerate(['言語', 'インフラ', 'コンテナ', 'データベース']):
    y = 30 + i * 150
    cell(f'row{i}', '', 30, y, W - 60, 130, 'rounded=1;arcSize=8;fillColor=#f5f8ff;strokeColor=#c4d6f5;strokeWidth=1.5;')
    text(f'row{i}-title', title, 30, y + 51, 150, 28, 22, True, '#2563eb')

ROW = 0
logo('csharp', 'csharp.svg', 'C#', 260, 66)
logo('php', 'php.svg', 'PHP', 420, 64, 118)
cell('sep', '', 530, 48, 1, 94, 'fillColor=#c4d6f5;strokeColor=none;')
logo('go', 'go.svg', 'Go（一部の案件）', 650, 40, 106)

ROW = 1
logo('aws', 'aws.png', 'AWS', 260, 62, 99)
logo('gcloud', 'google-cloud.png', 'Google Cloud', 420, 66)

ROW = 2
logo('docker', 'docker.svg', 'Docker', 260, 62, 79)
logo('k8s', 'kubernetes.svg', 'Kubernetes', 420, 66)

ROW = 3
logo('mysql', 'mysql.svg', 'MySQL', 260, 66, 97)
logo('spanner', 'spanner.svg', 'Spanner', 420, 66)

ET.indent(doc, space='  ')
ET.ElementTree(doc).write(BASE / 'tech-stack.drawio', encoding='utf-8', xml_declaration=True)
