"""Rebuild the authored example campaign and the editor's offline copy."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def amount(resource, value): return {"resourceId": resource, "amount": value}
def choice(id, text, result, costs=(), rewards=()):
    return dict(id=id, text=text, result=result, costs=list(costs), rewards=list(rewards))

titles = ['星球解体', '陌生访客', '舱体撞击', '赤红星域', '交战空域', '轨道加油站', '星海鲸群', '另一艘你', '耗材分拣站']
descriptions = [
    '观测星球正在崩解。舷窗外的裂缝迅速扩张，碎片散入航路。记录这一切，或冒险投入补给收集样本。',
    '一艘不明飞行物停在观察窗外，驾驶员正在挥手。舱内终端未找到可用的交流协议。',
    '撞击切断了主电路。前往 B 墙取下维修锤，再修复 D 墙的三处裂痕。全部修复后才能继续航行。',
    '跃迁终点是一片赤红星域。熔岩世界之间，巨大的眼睛正注视着探测舱。选择记录，或者消耗补给进行深度扫描。',
    '红蓝战机持续交火。你只是路过的生物传感器，没有收到参战指令。观察弹道，寻找有价值的残骸。',
    '加油站的管线仍在工作。无人系统接受回收材料作为交换，排队的船只似乎已经等待了很久。',
    '巨鲸穿过星海，幼鲸从舱壁游入，又穿过另一面舱壁离开。仪表未检测到实体碰撞。记录它们的迁徙轨迹。',
    '另一艘同编号探测舱停在窗外。驾驶员延迟半秒模仿你的动作，随后自行灭灯，伸手贴向窗户。',
    '机械流水线将探测舱逐一夹持、扫描和贴牌。相邻舱体被送入回收通道。等候夹钳松开，接受下一次投放。'
]
events = []
durations = [6, 6, 4, 8, 6, 8, 12, 16, 25]
for i, (title, description) in enumerate(zip(titles, descriptions), 1):
    options = [choice('record', '记录观测并继续', '观测记录已收入航行日志。', rewards=[amount('data', 3)])]
    if i == 6:
        options.insert(0, choice('refuel', '用回收材料换取燃料', '补给管线已完成输送。', [amount('scrap', 4)], [amount('fuel', 6)]))
    elif i == 3:
        options[0] = choice('repair', '确认维修完成，回收损坏零件', '三处裂痕完成封补，应急电路恢复。', rewards=[amount('scrap', 3)])
    else:
        options.insert(0, choice('survey', '投入补给，深入采集', '额外样本和传感器数据已封存。', [amount('supplies', 2)], [amount('data', 7), amount('scrap', 2)]))
    events.append(dict(id=f'show_{i}', type='setpiece', title=title, description=description, setpiece=i, duration=durations[i-1], choices=options))
events.extend([
    dict(id='salvage', type='choice', title='漂流补给箱', description='一个旧式补给箱正在低速漂流。只能带走其中一组物资，剩余部分将继续留在航道上。', setpiece=0, duration=0, choices=[
        choice('fuel', '回收燃料罐', '已回收可用燃料。', rewards=[amount('fuel', 3)]),
        choice('rations', '回收口粮和材料', '已封存口粮与零件。', rewards=[amount('supplies', 5), amount('scrap', 3)])]),
    dict(id='trade', type='choice', title='自动补给信标', description='信标提供一次物资交换。也可以领取紧急燃料包后离开。', setpiece=0, duration=0, choices=[
        choice('purchase', '交换燃料与补给', '材料交换完成。', [amount('scrap', 3)], [amount('fuel', 5), amount('supplies', 4)]),
        choice('emergency', '领取紧急燃料', '紧急燃料已转入储存罐。', rewards=[amount('fuel', 2)])]),
    dict(id='relay', type='choice', title='残缺的数据中继', description='旧中继愿意接收观测数据，换取下一段航路的物资配额。也可以保留数据。', setpiece=0, duration=0, choices=[
        choice('upload', '提交 5 份数据，换取补给', '观测数据已上传，补给配额已到账。', [amount('data', 5)], [amount('supplies', 5), amount('scrap', 4)]),
        choice('keep', '保留数据，继续航行', '中继信号逐渐远去。')])
])
sectors = []
for i, name in enumerate(['边缘观测带', '赤红航道', '回收边界']):
    def node(id, name, x, y, event='', next=(), exit=False): return dict(id=id, name=name, x=x, y=y, eventId=event, next=list(next), exit=exit)
    sectors.append(dict(id=f'sector_{i+1}', name=name, description='探测舱航路 / 请选择一条分支并向右侧出口前进。', startNodeId='entry', nextSectorId=f'sector_{i+2}' if i < 2 else '', nodes=[
        node('entry', '跃迁入口', 0, .5, next=['signal_a','signal_b','signal_c']),
        node('signal_a', titles[i*3], .25, .08, f'show_{i*3+1}', ['salvage','trade']),
        node('signal_b', titles[i*3+1], .25, .5, f'show_{i*3+2}', ['salvage','trade']),
        node('signal_c', titles[i*3+2], .25, .92, f'show_{i*3+3}', ['salvage','trade']),
        node('salvage', '漂流物资', .50, .23, 'salvage', ['relay']),
        node('trade', '补给信标', .50, .77, 'trade', ['relay']),
        node('relay', '数据中继', .76, .5, 'relay', ['exit']),
        node('exit', '星域出口', 1, .5, exit=True)
    ]))
campaign = dict(schemaVersion=1, id='astra_first_voyage', name='探测任务 04 · 深空航程', startSectorId='sector_1', resources=[
    dict(id='fuel', name='燃料', initial=10, capacity=24), dict(id='supplies', name='补给', initial=18, capacity=30),
    dict(id='scrap', name='材料', initial=8, capacity=99), dict(id='data', name='数据', initial=0, capacity=99)
], travelCosts=[amount('fuel', 1), amount('supplies', 1)], sectors=sectors, events=events)
encoded = json.dumps(campaign, ensure_ascii=False, indent=2)
path = ROOT/'UnityProject/Assets/StreamingAssets/Navigation/campaign.json'
path.parent.mkdir(parents=True, exist_ok=True); path.write_text(encoded, encoding='utf-8')
editor = ROOT/'Tools/StarMapEditor'
editor.mkdir(parents=True, exist_ok=True)
(editor/'default-campaign.js').write_text('window.DEFAULT_CAMPAIGN = ' + encoded + ';\n', encoding='utf-8')
(editor/'campaign.json').write_text(encoded, encoding='utf-8')
print('Created sample: 3 sectors, 24 nodes, 12 events, 9 setpieces.')
