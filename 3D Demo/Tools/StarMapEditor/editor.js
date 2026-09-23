/* Offline authoring surface. Imported strings are always rendered as text, never HTML. */
(() => {
  'use strict';
  const C = NavEditorCore, $ = id => document.getElementById(id), STORAGE = 'astra-navigation-editor-v1';
  let campaign = C.clone(DEFAULT_CAMPAIGN), sectorId = campaign.startSectorId, nodeId = 'entry', mode = 'node', eventId = null;
  const undo = [], redo = []; let drag = null, linkFrom = null;
  try {
    const draft = localStorage.getItem(STORAGE);
    if (draft) {
      const candidate = JSON.parse(draft), list = value => Array.isArray(value) && value.every(v => v && typeof v === 'object');
      if (!candidate || !list(candidate.sectors) || !candidate.sectors.length || !list(candidate.resources) || !candidate.resources.length || !list(candidate.events) || !Array.isArray(candidate.travelCosts) ||
          !candidate.sectors.every(s => list(s.nodes) && s.nodes.length && s.nodes.every(n => Array.isArray(n.next))) ||
          !candidate.events.every(e => list(e.choices) && e.choices.every(o => Array.isArray(o.costs) && Array.isArray(o.rewards)))) throw Error('草稿结构损坏');
      campaign = candidate; sectorId = campaign.startSectorId; status('已恢复本机草稿。导出前请处理校验结果。');
    }
  } catch (_) { status('本机草稿无法读取，已使用示例。原草稿不会自动覆盖，直至再次编辑。', true); }
  const sector = () => campaign.sectors.find(s => s.id === sectorId) || campaign.sectors[0];
  const node = () => sector().nodes.find(n => n.id === nodeId);
  function element(tag, text, className) { const el = document.createElement(tag); if (text != null) el.textContent = text; if (className) el.className = className; return el; }
  function status(text, error = false) { $('status').textContent = text; $('status').classList.toggle('toast-error', error); }
  function persist() {
    try { localStorage.setItem(STORAGE, JSON.stringify(campaign)); status('草稿已保存在本机 · ' + new Date().toLocaleTimeString()); }
    catch (_) { status('浏览器无法保存草稿，请导出 JSON 保留改动。', true); }
  }
  function mutate(change) {
    const before = JSON.stringify(campaign);
    try { change(); const after = JSON.stringify(campaign); if (before !== after) { undo.push(before); if (undo.length > 80) undo.shift(); redo.length = 0; persist(); } render(); }
    catch (error) { campaign = JSON.parse(before); render(); status(error.message, true); }
  }
  function history(from, to) { if (!from.length) return; to.push(JSON.stringify(campaign)); campaign = JSON.parse(from.pop()); sectorId = sector().id; nodeId = sector().startNodeId; mode = 'node'; persist(); render(); }
  function field(parent, title, value, change, options = {}) {
    const label = element('label', title, 'field'); let input;
    if (options.select) {
      input = element('select'); options.select.forEach(([id, name]) => { const o = element('option', name); o.value = id; input.append(o); });
    } else if (options.multiline) input = element('textarea');
    else { input = element('input'); input.type = options.number ? 'number' : 'text'; if (options.number) { input.step = options.step || '1'; if (options.min != null) input.min = options.min; if (options.max != null) input.max = options.max; } }
    input.value = value == null ? '' : value; input.setAttribute('aria-label', title);
    if (options.readonly) input.readOnly = true;
    input.addEventListener('change', () => mutate(() => change(options.number ? Number(input.value) : input.value)));
    label.append(input); parent.append(label); return input;
  }
  function button(parent, text, action, className = '') { const b = element('button', text, className); b.addEventListener('click', action); parent.append(b); return b; }
  function check(parent, text, value, action) { const label = element('label', null, 'check'), input = element('input'); input.type = 'checkbox'; input.checked = value; input.addEventListener('change', () => mutate(() => action(input.checked))); label.append(input, document.createTextNode(text)); parent.append(label); }
  function help(parent, text) { parent.append(element('p', text, 'help')); }
  function title(parent, eyebrow, text) { parent.append(element('div', eyebrow, 'eyebrow'), element('h3', text)); }
  function heading(parent, text) { parent.append(element('h4', text)); }
  function setAmount(list, resourceId, amount) { const index = list.findIndex(a => a.resourceId === resourceId); if (index >= 0) list.splice(index, 1); if (amount !== 0) list.push({ resourceId, amount }); }
  function amounts(parent, titleText, values) {
    heading(parent, titleText); const grid = element('div', null, 'amount-grid'); parent.append(grid);
    campaign.resources.forEach(r => field(grid, r.name, (values.find(a => a.resourceId === r.id) || {}).amount || 0, value => setAmount(values, r.id, value), { number: true, min: 0, max: 1000000 }));
  }
  function render() {
    if (!campaign.sectors.some(s => s.id === sectorId)) sectorId = campaign.sectors[0].id;
    $('campaign-title').textContent = campaign.name;
    $('sector-title').textContent = sector().name; $('sector-index').textContent = 'SECTOR ' + String(campaign.sectors.indexOf(sector()) + 1).padStart(2, '0') + ' / 星域航路';
    $('sectors').replaceChildren();
    campaign.sectors.forEach((s, i) => {
      const b = button($('sectors'), `${String(i + 1).padStart(2, '0')}  ${s.name}`, () => { sectorId = s.id; nodeId = s.startNodeId; mode = 'node'; linkFrom = null; render(); }, 'sector-button' + (s.id === sectorId ? ' active' : ''));
      b.append(element('small', `${s.nodes.length} NODES / ${s.id}`));
    });
    $('undo').disabled = undo.length === 0; $('redo').disabled = redo.length === 0;
    renderMap(); renderInspector(); validate();
  }
  function validate() {
    const errors = C.validate(campaign); $('validation-state').textContent = errors.length ? `${errors.length} 项配置需要修正` : '✓ 配置有效，可以导入游戏';
    $('validation-state').classList.toggle('invalid', !!errors.length);
    $('statistics').textContent = `${campaign.sectors.length} 星域 · ${campaign.sectors.reduce((n, s) => n + s.nodes.length, 0)} 信标 · ${campaign.events.length} 事件`;
    $('errors').replaceChildren(...errors.map(e => element('li', e))); $('export').disabled = !!errors.length;
    return errors;
  }
  const ns = 'http://www.w3.org/2000/svg';
  function svg(tag, attrs, text) { const el = document.createElementNS(ns, tag); Object.entries(attrs || {}).forEach(([key, value]) => el.setAttribute(key, value)); if (text != null) el.textContent = text; return el; }
  const point = n => ({ x: 75 + n.x * 850, y: 65 + n.y * 465 });
  function renderMap() {
    const map = $('map'); map.replaceChildren(); const defs = svg('defs');
    const marker = svg('marker', { id: 'arrow', viewBox: '0 0 10 10', refX: 9, refY: 5, markerWidth: 5, markerHeight: 5, orient: 'auto-start-reverse' });
    marker.append(svg('path', { d: 'M 0 0 L 10 5 L 0 10 z', fill: '#849875' })); defs.append(marker); map.append(defs);
    for (let i = 1; i < 10; i++) map.append(svg('line', { x1: i * 100, x2: i * 100, y1: 0, y2: 620, class: 'grid' }));
    for (let i = 1; i < 7; i++) map.append(svg('line', { x1: 0, x2: 1000, y1: i * 90, y2: i * 90, class: 'grid' }));
    for (let i = 0; i < 85; i++) map.append(svg('circle', { cx: (i * 127 + 23) % 987, cy: (i * 211 + 11) % 605, r: i % 5 === 0 ? 1.4 : .7, fill: '#70846e', opacity: .45 }));
    const s = sector();
    s.nodes.forEach(n => n.next.forEach(id => {
      const target = s.nodes.find(v => v.id === id); if (!target) return;
      const a = point(n), b = point(target), length = Math.hypot(b.x - a.x, b.y - a.y) || 1, dx = (b.x - a.x) / length * 21, dy = (b.y - a.y) / length * 21;
      map.append(svg('line', { x1: a.x + dx, y1: a.y + dy, x2: b.x - dx, y2: b.y - dy, class: 'edge', 'marker-end': 'url(#arrow)' }));
    }));
    s.nodes.forEach(n => {
      const p = point(n), ev = campaign.events.find(e => e.id === n.eventId), isStart = n.id === s.startNodeId;
      const color = isStart ? '#d5c27c' : n.exit ? '#d7a379' : ev && ev.type === 'setpiece' ? '#a9cc9b' : '#83b3b0';
      const g = svg('g', { transform: `translate(${p.x},${p.y})`, class: 'node' + (n.id === nodeId ? ' selected' : ''), 'data-node': n.id, tabindex: 0, role: 'button', 'aria-label': '信标 ' + n.name });
      g.append(svg('circle', { r: 29, class: 'halo' }), svg('circle', { r: 17, fill: '#16241b', stroke: color, 'stroke-width': 2 }), svg('text', { y: 6, 'text-anchor': 'middle', fill: color, 'font-size': 18 }, isStart ? '○' : n.exit ? '›' : ev && ev.type === 'setpiece' ? '!' : '+'));
      g.append(svg('text', { y: 48, 'text-anchor': 'middle', fill: color, 'font-size': 16 }, n.name.length > 10 ? n.name.slice(0, 10) + '…' : n.name));
      g.append(svg('text', { y: 67, 'text-anchor': 'middle', fill: '#738970', 'font-size': 10, 'font-family': 'monospace' }, n.id));
      g.addEventListener('pointerdown', event => {
        if (event.button !== 0) return;
        event.preventDefault();
        if (event.shiftKey) {
          if (!linkFrom) { linkFrom = n.id; nodeId = n.id; render(); status('已选连线起点，再按住 Shift 点击目标节点。'); }
          else { const from = linkFrom; linkFrom = null; mutate(() => C.toggleEdge(s, from, n.id)); }
          return;
        }
        linkFrom = null; nodeId = n.id; mode = 'node';
        drag = { id: n.id, before: JSON.stringify(campaign), pointer: event.pointerId, moved: false };
        map.setPointerCapture(event.pointerId); renderMap(); renderInspector();
      });
      g.addEventListener('keydown', event => {
        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); nodeId = n.id; mode = 'node'; render(); }
      });
      map.append(g);
    });
  }
  function mapPoint(event) { const p = new DOMPoint(event.clientX, event.clientY).matrixTransform($('map').getScreenCTM().inverse()); return { x: Math.max(0, Math.min(1, (p.x - 75) / 850)), y: Math.max(0, Math.min(1, (p.y - 65) / 465)) }; }
  $('map').addEventListener('pointermove', event => {
    if (!drag) return; const p = mapPoint(event), n = sector().nodes.find(n => n.id === drag.id);
    n.x = Math.round(p.x * 1000) / 1000; n.y = Math.round(p.y * 1000) / 1000; drag.moved = true; renderMap();
  });
  function endDrag(event) {
    if (!drag) return;
    if (drag.moved && drag.before !== JSON.stringify(campaign)) { undo.push(drag.before); if (undo.length > 80) undo.shift(); redo.length = 0; persist(); }
    if ($('map').hasPointerCapture(drag.pointer)) $('map').releasePointerCapture(drag.pointer);
    drag = null; render();
  }
  $('map').addEventListener('pointerup', endDrag); $('map').addEventListener('pointercancel', endDrag);
  $('map').addEventListener('dblclick', event => { if (!event.target.closest('[data-node]')) addNode(mapPoint(event)); });
  function addNode(p = { x: .5, y: .5 }) {
    mutate(() => {
      const s = sector(); if (s.nodes.length >= 128) throw Error('每个星域最多 128 个节点');
      nodeId = C.unique('node_', s.nodes); s.nodes.push({ id: nodeId, name: '新信标', x: p.x, y: p.y, eventId: campaign.events[0]?.id || '', next: [], exit: false }); mode = 'node';
    });
    status('信标已新增。请配置进入和离开它的航线。');
  }
  function renderInspector() {
    const panel = $('inspector'); panel.replaceChildren();
    if (mode === 'campaign') return campaignInspector(panel);
    if (mode === 'sector') return sectorInspector(panel);
    if (mode === 'library') return libraryInspector(panel);
    if (mode === 'event') return eventInspector(panel);
    const n = node(); if (!n) { help(panel, '选择地图上的信标，查看事件与航线。'); return; }
    title(panel, 'BEACON / 信标', n.name);
    field(panel, '节点 ID', n.id, value => { C.renameNode(sector(), n.id, value); nodeId = value; });
    field(panel, '显示名称', n.name, value => n.name = value);
    const pos = element('div', null, 'row'); panel.append(pos);
    field(pos, '横坐标 X', n.x, value => n.x = value, { number: true, step: '.01', min: 0, max: 1 });
    field(pos, '纵坐标 Y', n.y, value => n.y = value, { number: true, step: '.01', min: 0, max: 1 });
    check(panel, '星域出口', n.exit, value => { n.exit = value; if (value) n.next = []; });
    if (sector().startNodeId === n.id) help(panel, '此节点是星域起点，不触发事件。');
    else {
      field(panel, '关联事件', n.eventId, value => n.eventId = value, { select: [['', '无事件（仅起点 / 出口）'], ...campaign.events.map(e => [e.id, e.title])] });
      if (n.eventId) button(panel, '编辑事件内容 →', () => { mode = 'event'; eventId = n.eventId; renderInspector(); }, 'wide');
      button(panel, '＋ 为此信标新建事件', () => addEvent(true), 'wide');
    }
    heading(panel, '出站航线');
    if (n.exit) help(panel, '出口直接连接下一星域，请在星域设置中指定。');
    else sector().nodes.filter(target => target.id !== n.id).forEach(target => check(panel, target.name, n.next.includes(target.id), value => { if (value) n.next.push(target.id); else n.next = n.next.filter(id => id !== target.id); }));
    panel.append(element('hr'));
    const del = button(panel, '删除此信标', () => { if (confirm('删除「' + n.name + '」及相关连线？此操作可撤销。')) mutate(() => { C.removeNode(sector(), n.id); nodeId = sector().startNodeId; }); }, 'danger wide');
    del.disabled = sector().startNodeId === n.id; if (del.disabled) help(panel, '要删除起点，请先在星域设置中选择另一个起点。');
  }
  function sectorInspector(panel) {
    const s = sector(); title(panel, 'SECTOR / 星域', '星域设置');
    field(panel, '星域 ID', s.id, value => {
      if (!/^[A-Za-z0-9_-]{1,64}$/.test(value) || campaign.sectors.some(v => v !== s && v.id === value)) throw Error('星域 ID 无效或重复');
      const old = s.id; s.id = value; campaign.sectors.forEach(v => { if (v.nextSectorId === old) v.nextSectorId = value; }); if (campaign.startSectorId === old) campaign.startSectorId = value; sectorId = value;
    });
    field(panel, '星域名称', s.name, value => s.name = value);
    field(panel, '星域描述', s.description, value => s.description = value, { multiline: true });
    field(panel, '起始信标', s.startNodeId, value => { s.startNodeId = value; const n = s.nodes.find(n => n.id === value); n.eventId = ''; n.exit = false; }, { select: s.nodes.map(n => [n.id, n.name]) });
    field(panel, '下一星域', s.nextSectorId, value => s.nextSectorId = value, { select: [['', '航程终点'], ...campaign.sectors.filter(v => v !== s).map(v => [v.id, v.name])] });
    help(panel, '星域内的连线必须形成无环航路。每个节点都应可从起点抵达，所有末端必须标记为出口。');
    const del = button(panel, '删除星域及其中节点', () => { if (confirm('删除「' + s.name + '」？事件库会保留，航路将接到下一星域。')) mutate(() => {
      campaign.sectors.forEach(v => { if (v.nextSectorId === s.id) v.nextSectorId = s.nextSectorId; });
      campaign.sectors = campaign.sectors.filter(v => v !== s); if (campaign.startSectorId === s.id) campaign.startSectorId = s.nextSectorId || campaign.sectors[0].id;
      sectorId = campaign.startSectorId; nodeId = sector().startNodeId;
    }); }, 'danger wide'); del.disabled = campaign.sectors.length <= 1;
  }
  function campaignInspector(panel) {
    title(panel, 'CAMPAIGN / 航程', '航程与资源');
    field(panel, '航程 ID', campaign.id, value => campaign.id = value);
    field(panel, '航程名称', campaign.name, value => campaign.name = value);
    field(panel, '起始星域', campaign.startSectorId, value => campaign.startSectorId = value, { select: campaign.sectors.map(s => [s.id, s.name]) });
    amounts(panel, '每次跃迁消耗', campaign.travelCosts);
    heading(panel, '资源定义');
    campaign.resources.forEach(r => {
      const group = element('div', null, 'resource-row'); panel.append(group);
      field(group, '资源 ID', r.id, value => {
        if (!/^[A-Za-z0-9_-]{1,64}$/.test(value) || campaign.resources.some(v => v !== r && v.id === value)) throw Error('资源 ID 无效或重复');
        const old = r.id; r.id = value; const lists = [campaign.travelCosts, ...campaign.events.flatMap(e => e.choices.flatMap(o => [o.costs, o.rewards]))];
        lists.forEach(list => list.forEach(a => { if (a.resourceId === old) a.resourceId = value; }));
      });
      field(group, '资源名称', r.name, value => r.name = value);
      const row = element('div', null, 'row'); group.append(row);
      field(row, '初始值', r.initial, value => r.initial = value, { number: true, min: 0 });
      field(row, '容量上限', r.capacity, value => r.capacity = value, { number: true, min: 1 });
      const del = button(group, '删除资源', () => { if (confirm('删除资源「' + r.name + '」及全部相关收支？')) mutate(() => {
        campaign.resources = campaign.resources.filter(v => v !== r); campaign.travelCosts = campaign.travelCosts.filter(a => a.resourceId !== r.id);
        campaign.events.forEach(e => e.choices.forEach(o => { o.costs = o.costs.filter(a => a.resourceId !== r.id); o.rewards = o.rewards.filter(a => a.resourceId !== r.id); }));
      }); }, 'small danger'); del.disabled = campaign.resources.length <= 1;
    });
    button(panel, '＋ 添加资源', () => mutate(() => { if (campaign.resources.length >= 6) throw Error('最多支持 6 类资源'); campaign.resources.push({ id: C.unique('resource_', campaign.resources), name: '新资源', initial: 0, capacity: 99 }); }), 'wide');
  }
  function libraryInspector(panel) {
    title(panel, 'EVENTS / 事件库', '事件库');
    help(panel, '同一个事件可以被多个节点引用。修改共享事件会影响所有引用节点，可先复制为独立事件。');
    button(panel, '＋ 新建事件', () => addEvent(false), 'primary wide');
    campaign.events.forEach(e => {
      const b = button(panel, e.title, () => { eventId = e.id; mode = 'event'; renderInspector(); }, 'event-item');
      b.append(element('small', (e.type === 'setpiece' ? '演出 ' + e.setpiece : '抉择') + ' / ' + e.id));
    });
  }
  function addEvent(bind) {
    mutate(() => {
      if (campaign.events.length >= 256) throw Error('最多支持 256 个事件');
      eventId = C.unique('event_', campaign.events); campaign.events.push({ id: eventId, type: 'choice', title: '新的遭遇', description: '在这里描述玩家遇到的事件。', setpiece: 1, duration: 8,
        choices: [{ id: 'continue', text: '记录并离开', result: '事件已记录。', costs: [], rewards: [] }] });
      if (bind && node()) node().eventId = eventId; mode = 'event';
    });
  }
  function eventInspector(panel) {
    const e = campaign.events.find(e => e.id === eventId); if (!e) { mode = 'library'; return libraryInspector(panel); }
    title(panel, 'EVENT / 事件', e.title);
    const refs = campaign.sectors.flatMap(s => s.nodes.filter(n => n.eventId === e.id).map(n => s.name + ' / ' + n.name));
    help(panel, '引用此事件：' + (refs.join('、') || '暂无节点'));
    field(panel, '事件 ID', e.id, value => {
      if (!/^[A-Za-z0-9_-]{1,64}$/.test(value) || campaign.events.some(v => v !== e && v.id === value)) throw Error('事件 ID 无效或重复');
      campaign.sectors.forEach(s => s.nodes.forEach(n => { if (n.eventId === e.id) n.eventId = value; })); e.id = value; eventId = value;
    });
    field(panel, '事件标题', e.title, value => e.title = value);
    field(panel, '事件类型', e.type, value => { e.type = value; if (value === 'setpiece') { e.setpiece = e.setpiece || 1; e.duration = e.duration || 8; } }, { select: [['choice', '文字选择 / 资源流转'], ['setpiece', '舱内演出 + 处置选择']] });
    if (e.type === 'setpiece') {
      const names = ['星球解体','陌生访客','撞击维修','赤红星域','战机交火','加油站','星海鲸群','另一艘你','耗材分拣站'];
      field(panel, '演出内容', e.setpiece, value => e.setpiece = Number(value), { select: names.map((name, i) => [String(i + 1), `${i + 1} · ${name}`]) });
      field(panel, '最短观看时间（秒）', e.duration, value => e.duration = value, { number: true, min: 1, max: 300, step: '.5' });
      if (e.setpiece === 3) help(panel, '撞击事件还要求玩家实际修复全部三处裂痕。');
    }
    field(panel, '事件描述', e.description, value => e.description = value, { multiline: true });
    heading(panel, '处置选项'); help(panel, '至少保留一个无消耗选项。0 表示无收支，收益超过容量的部分会舍弃。');
    e.choices.forEach((o, index) => {
      const group = element('div', null, 'choice-block'); panel.append(group); heading(group, '选项 ' + (index + 1));
      field(group, '选项 ID', o.id, value => o.id = value);
      field(group, '按钮文字', o.text, value => o.text = value);
      field(group, '结算叙述', o.result, value => o.result = value, { multiline: true });
      amounts(group, '消耗', o.costs); amounts(group, '收益', o.rewards);
      const del = button(group, '删除选项', () => mutate(() => e.choices.splice(index, 1)), 'small danger'); del.disabled = e.choices.length <= 1;
    });
    button(panel, '＋ 增加选项', () => mutate(() => { if (e.choices.length >= 8) throw Error('每个事件最多 8 个选项'); e.choices.push({ id: C.unique('option_', e.choices), text: '新的选择', result: '选择已记录。', costs: [], rewards: [] }); }), 'wide');
    button(panel, '复制为独立事件', () => mutate(() => {
      if (campaign.events.length >= 256) throw Error('事件库已满'); const copy = C.clone(e); copy.id = C.unique('event_', campaign.events); copy.title += '（副本）'; campaign.events.push(copy);
      if (node() && node().eventId === e.id) node().eventId = copy.id; eventId = copy.id;
    }), 'wide');
    const del = button(panel, '删除事件', () => { if (confirm('删除未引用事件「' + e.title + '」？')) mutate(() => { campaign.events = campaign.events.filter(v => v !== e); mode = 'library'; }); }, 'danger wide'); del.disabled = !!refs.length;
    if (refs.length) help(panel, '先移除所有节点引用后，才能删除事件。');
  }
  function helpDialog() {
    const body = $('modal-body'); body.replaceChildren(element('h2', '将星域配置导入游戏'));
    const list = element('ol');
    ['完成星域、节点、连线和事件设置；处理地图下方的所有校验错误。', '点击「导出到游戏」，保存下载的 campaign.json。', '将文件复制到游戏旁 Astra Cabin_Data / StreamingAssets / Navigation 文件夹，替换同名 campaign.json。', '打开游戏 A 墙电脑 → 导航模式 → 导入星域配置 → 校验并导入。旧航程会备份，并开始新航程。'].forEach(t => list.append(element('li', t)));
    body.append(list, element('p', 'Unity 工程中对应路径：Assets/StreamingAssets/Navigation/campaign.json。游戏启动后优先继续已导入的配置；修改文件后需要在导航模式中再次导入。'), element('p', '演出编号 1–9 直接复用现有舱内演出。新增代码事件类型需在游戏 EventRegistry 注册处理器，并扩展此编辑器的类型选项和校验。'));
    $('modal').showModal();
  }
  $('close-dialog').onclick = () => $('modal').close(); $('help').onclick = helpDialog;
  $('load').onclick = () => $('file').click();
  async function importFile(file) {
    if (!file) return;
    try {
      if (file.size > 2 * 1024 * 1024) throw Error('文件超过 2 MB');
      const next = C.parse(await file.text());
      if (!confirm('载入「' + next.name + '」并替换当前草稿？可以撤销。')) return;
      mutate(() => { campaign = next; sectorId = next.startSectorId; nodeId = sector().startNodeId; mode = 'node'; }); status('已打开配置：' + file.name);
    } catch (error) { status('导入失败，原草稿保留：' + error.message, true); const body = $('modal-body'); body.replaceChildren(element('h2', '配置未导入'), element('p', error.message)); $('modal').showModal(); }
  }
  $('file').onchange = event => { importFile(event.target.files[0]); event.target.value = ''; };
  document.addEventListener('dragover', event => event.preventDefault());
  document.addEventListener('drop', event => { event.preventDefault(); importFile(event.dataTransfer.files[0]); });
  $('export').onclick = () => {
    if (validate().length) { status('请先修正配置错误。', true); return; }
    const blob = new Blob([JSON.stringify(campaign, null, 2)], { type: 'application/json;charset=utf-8' }), url = URL.createObjectURL(blob), anchor = element('a');
    anchor.href = url; anchor.download = 'campaign.json'; document.body.append(anchor); anchor.click(); anchor.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000); status('已导出 campaign.json，请放入游戏配置目录并导入。'); helpDialog();
  };
  $('undo').onclick = () => history(undo, redo); $('redo').onclick = () => history(redo, undo);
  $('example').onclick = () => { if (confirm('用三个星域的示例替换当前草稿？可以撤销。')) mutate(() => { campaign = C.clone(DEFAULT_CAMPAIGN); sectorId = campaign.startSectorId; nodeId = 'entry'; mode = 'node'; }); };
  $('settings').onclick = () => { mode = 'campaign'; renderInspector(); };
  $('sector-settings').onclick = () => { mode = 'sector'; renderInspector(); };
  $('event-library').onclick = () => { mode = 'library'; renderInspector(); };
  $('add-node').onclick = () => addNode();
  $('add-sector').onclick = () => mutate(() => {
    if (campaign.sectors.length >= 16) throw Error('最多支持 16 个星域');
    const previous = sector(), id = C.unique('sector_', campaign.sectors), next = previous.nextSectorId; previous.nextSectorId = id;
    campaign.sectors.push({ id, name: '新星域', description: '', startNodeId: 'entry', nextSectorId: next, nodes: [
      { id: 'entry', name: '跃迁入口', x: 0, y: .5, eventId: '', next: ['exit'], exit: false },
      { id: 'exit', name: '星域出口', x: 1, y: .5, eventId: '', next: [], exit: true }
    ] }); sectorId = id; nodeId = 'entry'; mode = 'sector';
  });
  document.addEventListener('keydown', event => {
    if (event.target.matches('input,textarea,select')) return;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') { event.preventDefault(); if (event.shiftKey) history(redo, undo); else history(undo, redo); }
    if (event.key === 'Escape') { linkFrom = null; status('已取消连线操作。'); }
  });
  render();
})();
