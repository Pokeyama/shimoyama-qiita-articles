"""Build editable draw.io sources; export their PNGs with draw.io Desktop."""
from pathlib import Path
import base64
import xml.etree.ElementTree as ET

BASE = Path(__file__).resolve().parent
BLUE, GREEN, ORANGE, GRAY = '#2563eb', '#15803d', '#b45309', '#64748b'
FONT = 'fontFamily=Hiragino Sans;fontColor=#243247;fontSize=18;whiteSpace=wrap;html=0;'


class Diagram:
    def __init__(self, name, width, height):
        self.name = name
        self.doc = ET.Element('mxfile', host='app.diagrams.net', type='device')
        page = ET.SubElement(self.doc, 'diagram', id=name, name=name)
        model = ET.SubElement(page, 'mxGraphModel', grid='1', gridSize='10', page='1',
                              pageScale='1', pageWidth=str(width), pageHeight=str(height), background='#ffffff')
        self.root = ET.SubElement(model, 'root')
        ET.SubElement(self.root, 'mxCell', id='0')
        ET.SubElement(self.root, 'mxCell', id='1', parent='0')

    def box(self, key, text, x, y, w, h, style=''):
        cell = ET.SubElement(self.root, 'mxCell', id=key, value=text, vertex='1', parent='1',
                             style=FONT + 'rounded=1;arcSize=10;strokeWidth=1.5;' + style)
        ET.SubElement(cell, 'mxGeometry', x=str(x), y=str(y), width=str(w), height=str(h), **{'as': 'geometry'})
        return cell

    def text(self, key, text, x, y, w, h=28, size=18, color='#243247', bold=False, align='center'):
        return self.box(key, text, x, y, w, h,
                        f'fillColor=none;strokeColor=none;fontSize={size};fontColor={color};'
                        f'fontStyle={1 if bold else 0};align={align};verticalAlign=middle;')

    def icon(self, key, name, x, y, size=52, color=GRAY, width=None):
        ext = 'png' if name == 'mysql' else 'svg'
        data = (BASE / f'icons/{name}.{ext}').read_bytes()
        if ext == 'svg' and name != 'pubsub':
            data = data.replace(b'<svg ', f'<svg fill="{color}" '.encode(), 1)
        encoded = base64.b64encode(data).decode()
        return self.box(key, '', x, y, width or size, size,
                        f'shape=image;imageAspect=1;image=data:image/{"svg+xml" if ext == "svg" else "png"},{encoded};'
                        'fillColor=none;strokeColor=none;')

    def arrow(self, key, text, x1, y1, x2, y2, color=BLUE, dashed=False, points=(), end=True):
        cell = ET.SubElement(self.root, 'mxCell', id=key, value='', edge='1', parent='1',
                             style=FONT + f'endArrow={"block" if end else "none"};endFill=1;strokeWidth={2 if end else 1};'
                             f'strokeColor={color};dashed={1 if dashed else 0};rounded=0;')
        geo = ET.SubElement(cell, 'mxGeometry', relative='1', **{'as': 'geometry'})
        ET.SubElement(geo, 'mxPoint', x=str(x1), y=str(y1), **{'as': 'sourcePoint'})
        ET.SubElement(geo, 'mxPoint', x=str(x2), y=str(y2), **{'as': 'targetPoint'})
        if points:
            arr = ET.SubElement(geo, 'Array', **{'as': 'points'})
            for x, y in points:
                ET.SubElement(arr, 'mxPoint', x=str(x), y=str(y))
        if text:
            self.text(key + '-label', text, min(x1, x2), min(y1, y2) - 29, abs(x2 - x1), 26, 17, color)
        return cell

    def lane(self, key, name, title, x, y, end_y):
        self.icon(key, name, x - 24, y, 48)
        self.text(key + '-title', title, x - 90, y + 53, 180)
        self.arrow(key + '-lane', '', x, y + 87, x, end_y, '#cbd5e1', True, end=False)

    def save(self):
        ET.indent(self.doc, space='  ')
        ET.ElementTree(self.doc).write(BASE / f'{self.name}.drawio', encoding='utf-8', xml_declaration=True)


def basics():
    d = Diagram('pubsub-basics', 1120, 680)
    d.box('service', '', 295, 20, 520, 290, 'fillColor=#f5f8ff;strokeColor=#c4d6f5;')
    d.icon('brand', 'pubsub', 315, 35, 32)
    d.text('title', 'Pub/Sub', 358, 37, 160, 30, 24, BLUE, True, 'left')
    d.icon('publisher', 'server', 70, 155, 64)
    d.text('publisher-label', 'Publisher', 25, 225, 155)
    d.icon('topic', 'topic', 365, 155, 64, BLUE)
    d.text('topic-label', 'トピック', 315, 225, 165)
    d.text('subscription-label', 'サブスクリプション', 565, 65, 240)
    d.text('subscriber-label', 'Subscriber', 905, 65, 180)
    for key, y in [('a', 110), ('b', 220)]:
        d.icon('subscription-' + key, 'subscription', 655, y, 52, BLUE)
        d.icon('subscriber-' + key, 'worker', 970, y, 52)
        d.arrow('delivery-' + key, '', 717, y + 26, 955, y + 26)
        d.icon('message-' + key, 'message', 820, y - 5, 25, BLUE)
    d.arrow('publish', 'publish', 145, 187, 350, 187)
    d.icon('message', 'message', 232, 200, 28, BLUE)
    d.text('message-label', '注文ID', 205, 233, 85, 26, 16, BLUE)
    d.arrow('branch-a', '', 440, 187, 640, 136, points=[(540, 187), (540, 136)])
    d.arrow('branch-b', '', 440, 187, 640, 246, points=[(540, 187), (540, 246)])
    for name, x in [('pull', 25), ('push', 575)]:
        d.box(name + '-panel', '', x, 350, 520, 290,
              f'fillColor={"#f5f8ff" if name == "pull" else "#f8fafc"};strokeColor=#d6e0ef;')
        d.text(name + '-title', name, x + 22, 363, 95, 30, 24, BLUE, True, 'left')
        if name == 'pull':
            d.text('this-example', '今回', x + 111, 366, 60, 26, 16, GRAY)
        left, right = x + 65, x + 450
        d.lane(name + '-service', 'pubsub', 'Pub/Sub', left, 405, 615)
        d.lane(name + '-worker', 'worker', 'ワーカー', right, 405, 615)
        if name == 'pull':
            d.arrow('pull-request', 'リクエスト', right, 515, left, 515, GRAY)
            d.arrow('pull-data', '', left, 555, right, 555)
            d.icon('pull-message', 'message', x + 245, 525, 26, BLUE)
            d.arrow('pull-result', 'ack / nack', right, 605, left, 605, GREEN)
        else:
            d.arrow('push-data', 'HTTPS POST', left, 530, right, 530)
            d.icon('push-message', 'message', x + 245, 540, 26, BLUE)
            d.arrow('push-result', 'HTTP応答（ack / nack）', right, 605, left, 605, GREEN)
    d.save()


def architecture():
    d = Diagram('order-flow', 1120, 490)
    d.text('title', '注文処理', 25, 12, 250, 40, 26, bold=True, align='left')
    d.box('api-server', '', 255, 90, 155, 140,
          'fillColor=#f5f8ff;strokeColor=#b7cdf5;')
    d.text('api-server-label', 'APIサーバー', 235, 58, 195, 28, 18, BLUE, True)
    d.box('worker-server', '', 900, 90, 190, 220,
          'fillColor=#f8fafc;strokeColor=#cbd5e1;')
    d.text('worker-server-label', 'ワーカーサーバー', 880, 58, 230, 28, 18, GRAY, True)
    for key, name, label, x in [('client', 'person', '注文する人', 65), ('api', 'server', 'API', 300),
                                ('pubsub', 'pubsub', 'Pub/Sub', 600), ('worker', 'worker', 'ワーカー', 950)]:
        d.icon(key, name, x, 105, 64)
        d.text(key + '-label', label, x - 55, 177, 174)
    d.arrow('order', '注文', 140, 125, 285, 125)
    d.arrow('response', '受付結果', 285, 165, 140, 165, GREEN)
    d.arrow('publish', 'publish', 375, 137, 585, 137)
    d.arrow('deliver', '注文ID', 675, 137, 935, 137)
    d.icon('payload', 'message', 772, 154, 30, BLUE)
    d.icon('db', 'mysql', 615, 355, 65, width=96)
    d.text('db-label', 'MySQL', 570, 425, 190)
    d.arrow('insert', '', 332, 216, 610, 375, points=[(332, 375)])
    d.text('insert-label', 'pending', 375, 337, 170, 26, 18, BLUE)
    d.arrow('select', '', 950, 216, 720, 375, points=[(950, 375)])
    d.text('select-label', '参照・doneに更新', 735, 337, 220, 26, 18, BLUE)
    d.box('work', '重い処理', 910, 248, 145, 42,
          'fillColor=#fff4e5;strokeColor=#e4c492;fontSize=17;')
    d.save()


def commit_then_publish():
    d = Diagram('commit-then-publish', 1120, 710)
    d.text('title', 'COMMIT → publish', 25, 12, 600, 40, 26, bold=True, align='left')
    for key, name, title, x in [('api', 'server', 'API', 110), ('db', 'mysql', 'MySQL', 390),
                               ('pubsub', 'pubsub', 'Pub/Sub', 700), ('worker', 'worker', 'ワーカー', 1000)]:
        d.lane(key, name, title, x, 85, 335 if key == 'api' else 650)
    d.arrow('insert', 'INSERT', 110, 235, 390, 235)
    d.arrow('commit', 'COMMIT', 110, 295, 390, 295, GREEN)
    d.text('crash-marker', '×', 90, 315, 40, 40, 36, '#dc2626', True)
    d.text('crash-label', '停止', 20, 360, 180, 28, 18, '#dc2626')
    d.arrow('publish', 'publish（未実行）', 110, 435, 700, 435, GRAY, True)
    d.box('db-state', 'pending', 320, 585, 140, 45,
          'fillColor=#fff4e5;strokeColor=#e4c492;')
    d.text('no-message', '届かない', 910, 590, 180, 35, 20, '#dc2626')
    d.save()


def publish_then_commit():
    d = Diagram('publish-then-commit', 1120, 710)
    d.text('title', 'publish → COMMIT', 25, 12, 600, 40, 26, bold=True, align='left')
    for key, name, title, x in [('api', 'server', 'API', 110), ('db', 'mysql', 'MySQL', 390),
                               ('pubsub', 'pubsub', 'Pub/Sub', 700), ('worker', 'worker', 'ワーカー', 1000)]:
        d.lane(key, name, title, x, 85, 650)
    d.arrow('insert', 'INSERT', 110, 235, 390, 235)
    d.arrow('publish', 'publish', 110, 295, 700, 295)
    d.arrow('delivery', '注文ID', 700, 355, 1000, 355)
    d.arrow('select', 'SELECT', 1000, 410, 390, 410)
    d.arrow('missing', 'まだ見つからない', 390, 460, 1000, 460, ORANGE, True)
    d.arrow('nack', 'nack', 1000, 515, 700, 515, ORANGE)
    d.arrow('commit', 'COMMIT', 110, 570, 390, 570, GREEN)
    d.arrow('redelivery', '再送', 700, 625, 1000, 625)
    d.save()


def comparison():
    d = Diagram('sync-async', 1120, 620)
    for title, start in [('同期', 25), ('非同期', 575)]:
        d.box(title + '-panel', '', start, 20, 520, 555,
              'fillColor=#f8fafc;strokeColor=#d6e0ef;')
        d.text(title + '-title', title, start + 20, 32, 100, 35, 25, bold=True, align='left')
    d.lane('sync-client', 'person', '呼び出し元', 95, 95, 530)
    d.lane('sync-api', 'server', 'API', 455, 95, 530)
    d.arrow('sync-order', '注文', 95, 225, 455, 225)
    d.box('sync-work', '重い処理', 395, 290, 120, 105,
          'fillColor=#fff4e5;strokeColor=#e4c492;')
    d.arrow('sync-response', '処理結果', 455, 475, 95, 475, GREEN)
    d.lane('async-client', 'person', '呼び出し元', 650, 95, 530)
    d.lane('async-api', 'server', 'API', 840, 95, 530)
    d.lane('async-worker', 'worker', 'ワーカー', 1030, 95, 530)
    d.arrow('async-order', '注文', 650, 225, 840, 225)
    d.arrow('async-publish', '', 840, 280, 1030, 280)
    d.icon('async-pubsub', 'pubsub', 912, 247, 35)
    d.arrow('async-response', '受付結果', 840, 335, 650, 335, GREEN)
    d.box('async-work', '重い処理', 970, 360, 120, 105,
          'fillColor=#fff4e5;strokeColor=#e4c492;')
    d.text('done', 'done', 975, 485, 110, 28, 18, GREEN)
    d.save()


if __name__ == '__main__':
    basics()
    architecture()
    commit_then_publish()
    publish_then_commit()
    comparison()
