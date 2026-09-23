(function (root) {
  'use strict';
  const clone = value => JSON.parse(JSON.stringify(value));
  const idOK = value => typeof value === 'string' && /^[A-Za-z0-9_-]{1,64}$/.test(value);
  const textOK = (value, max) => typeof value === 'string' && value.trim().length > 0 && value.length <= max;
  const bounded = (value, min, max) => Number.isInteger(value) && value >= min && value <= max;
  function validate(c) {
    const errors = [];
    const error = text => errors.push(text);
    if (!c || typeof c !== 'object' || Array.isArray(c)) return ['配置必须是 JSON 对象'];
    if (c.schemaVersion !== 1) error('schemaVersion 必须为 1');
    if (!idOK(c.id) || !textOK(c.name, 80)) error('航程 ID 或名称无效');
    if (!Array.isArray(c.resources) || c.resources.length < 1 || c.resources.length > 6 || c.resources.some(r => !r) ||
        !Array.isArray(c.sectors) || c.sectors.length < 1 || c.sectors.length > 16 || c.sectors.some(s => !s) ||
        !Array.isArray(c.events) || c.events.length > 256 || c.events.some(e => !e))
      return errors.concat('资源需 1–6 项、星域需 1–16 项、事件最多 256 项，不可包含空项');
    const resources = new Set();
    c.resources.forEach(r => {
      if (!idOK(r.id) || resources.has(r.id) || !textOK(r.name, 12) || !bounded(r.capacity, 1, 1000000) || !bounded(r.initial, 0, r.capacity)) error('资源定义无效或重复：' + r.id);
      resources.add(r.id);
    });
    function amounts(values, where) {
      if (!Array.isArray(values) || values.length > 6) { error(where + '：资源列表无效'); return; }
      const seen = new Set();
      values.forEach(v => {
        if (!v || !resources.has(v.resourceId) || seen.has(v.resourceId) || !bounded(v.amount, 1, 1000000)) error(where + '：未知资源、重复项或非正整数');
        if (v) seen.add(v.resourceId);
      });
    }
    amounts(c.travelCosts, '航行消耗');
    const events = new Set();
    c.events.forEach(e => {
      if (!idOK(e.id) || events.has(e.id) || !textOK(e.title, 80) || !textOK(e.description, 4000)) error('事件定义无效或重复：' + e.id);
      events.add(e.id);
      if (!['choice', 'setpiece'].includes(e.type)) error(e.id + '：未知事件类型');
      if (e.type === 'setpiece' && (!bounded(e.setpiece, 1, 9) || !Number.isFinite(e.duration) || e.duration < 1 || e.duration > 300)) error(e.id + '：演出编号需 1–9，时长需 1–300 秒');
      if (!Array.isArray(e.choices) || e.choices.length < 1 || e.choices.length > 8 || e.choices.some(o => !o)) { error(e.id + '：选项需 1–8 项'); return; }
      const options = new Set();
      e.choices.forEach(o => {
        if (!idOK(o.id) || options.has(o.id) || !textOK(o.text, 160) || !textOK(o.result, 2000)) error(e.id + '：选项内容无效或 ID 重复');
        options.add(o.id); amounts(o.costs, e.id + '/' + o.id + ' 消耗'); amounts(o.rewards, e.id + '/' + o.id + ' 收益');
      });
      if (!e.choices.some(o => Array.isArray(o.costs) && o.costs.length === 0)) error(e.id + '：至少保留一个无消耗选项');
    });
    const sectors = new Map();
    c.sectors.forEach(s => { if (!idOK(s.id) || sectors.has(s.id)) error('星域 ID 无效或重复：' + s.id); sectors.set(s.id, s); });
    c.sectors.forEach(s => {
      const prefix = s.name || s.id;
      if (!textOK(s.name, 80) || (s.description != null && (typeof s.description !== 'string' || s.description.length > 2000))) error(prefix + '：星域文字无效');
      if (s.nextSectorId && !sectors.has(s.nextSectorId)) error(prefix + '：下一星域不存在');
      if (!Array.isArray(s.nodes) || s.nodes.length < 2 || s.nodes.length > 128 || s.nodes.some(n => !n)) { error(prefix + '：节点需 2–128 项'); return; }
      const nodes = new Map();
      s.nodes.forEach(n => {
        if (!idOK(n.id) || nodes.has(n.id)) error(prefix + '：节点 ID 无效或重复 ' + n.id);
        nodes.set(n.id, n);
        if (!textOK(n.name, 32) || !Number.isFinite(n.x) || !Number.isFinite(n.y) || n.x < 0 || n.x > 1 || n.y < 0 || n.y > 1) error(prefix + '/' + n.id + '：名称或坐标无效');
        if (n.eventId && !events.has(n.eventId)) error(prefix + '/' + n.id + '：事件不存在');
        if (!Array.isArray(n.next) || n.next.length > 128 || new Set(n.next).size !== n.next.length) error(prefix + '/' + n.id + '：连线数组无效或重复');
        if (n.exit && (!Array.isArray(n.next) || n.next.length)) error(prefix + '/' + n.id + '：出口不能有连线');
        if (!n.exit && n.id !== s.startNodeId && !n.eventId) error(prefix + '/' + n.id + '：普通节点需关联事件');
        if (!n.exit && Array.isArray(n.next) && !n.next.length) error(prefix + '/' + n.id + '：非出口节点不能是死路');
      });
      s.nodes.forEach(n => (Array.isArray(n.next) ? n.next : []).forEach(target => {
        if (!nodes.has(target) || target === n.id) error(prefix + '/' + n.id + '：连线目标无效');
      }));
      if (!nodes.has(s.startNodeId)) error(prefix + '：起点不存在');
      else {
        const start = nodes.get(s.startNodeId);
        if (start.exit || start.eventId) error(prefix + '：起点不能是出口或关联事件');
        const reached = new Set(), visiting = new Set(); let cycle = false;
        function walk(id) {
          if (!nodes.has(id)) return;
          if (visiting.has(id)) { cycle = true; return; }
          if (reached.has(id)) return;
          reached.add(id); visiting.add(id);
          (Array.isArray(nodes.get(id).next) ? nodes.get(id).next : []).forEach(walk); visiting.delete(id);
        }
        walk(s.startNodeId);
        if (cycle) error(prefix + '：航线有环');
        if (reached.size !== nodes.size) error(prefix + '：存在不可达节点');
        if (!s.nodes.some(n => n.exit)) error(prefix + '：缺少出口');
      }
    });
    if (!sectors.has(c.startSectorId)) error('起始星域不存在');
    else {
      let id = c.startSectorId; const seen = new Set();
      while (id && sectors.has(id)) { if (seen.has(id)) { error('星域顺序有环'); break; } seen.add(id); id = sectors.get(id).nextSectorId; }
      if (seen.size !== sectors.size) error('存在不可达星域');
    }
    return errors;
  }
  function parse(text) {
    if (new TextEncoder().encode(text).length > 2 * 1024 * 1024) throw Error('文件超过 2 MB');
    const data = JSON.parse(text), errors = validate(data);
    if (errors.length) throw Error(errors.slice(0, 8).join('\n'));
    return data;
  }
  function unique(prefix, records) { let i = 1; while (records.some(r => r.id === prefix + i)) i++; return prefix + i; }
  function removeNode(sector, id) { sector.nodes = sector.nodes.filter(n => n.id !== id); sector.nodes.forEach(n => { n.next = n.next.filter(x => x !== id); }); }
  function renameNode(sector, oldId, newId) {
    if (!idOK(newId) || sector.nodes.some(n => n.id === newId && n.id !== oldId)) throw Error('节点 ID 无效或重复');
    sector.nodes.find(n => n.id === oldId).id = newId;
    sector.nodes.forEach(n => { n.next = n.next.map(x => x === oldId ? newId : x); });
    if (sector.startNodeId === oldId) sector.startNodeId = newId;
  }
  function toggleEdge(sector, from, to) {
    const node = sector.nodes.find(n => n.id === from);
    if (!node || node.exit || from === to) throw Error('出口或节点自身不能连线');
    node.next = node.next.includes(to) ? node.next.filter(id => id !== to) : node.next.concat(to);
  }
  const api = { clone, validate, parse, unique, removeNode, renameNode, toggleEdge };
  if (typeof module !== 'undefined' && module.exports) module.exports = api; else root.NavEditorCore = api;
})(typeof window === 'undefined' ? globalThis : window);
