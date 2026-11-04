const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const C = require('./editor-core.js');
const source = JSON.parse(fs.readFileSync(path.join(__dirname, 'campaign.json'), 'utf8'));
let checks = 0;
function check(name, fn) { fn(); checks++; console.log('PASS ' + name); }
function invalid(name, change) { check('reject ' + name, () => { const data = C.clone(source); change(data); assert.ok(C.validate(data).length); }); }
check('travel label optional and round trips', () => {
  for (const value of [undefined, null, '', '   ', '继续漂流', '看“光”与\\航线', '字'.repeat(24)]) {
    const c=C.clone(source); if(value !== undefined)c.sectors[0].nodes[1].travelButtonText=value;
    assert.deepEqual(C.parse(JSON.stringify(c)),c);
  }
});
invalid('travel label too long', c => c.sectors[0].nodes[1].travelButtonText='字'.repeat(25));
invalid('travel label wrong type', c => c.sectors[0].nodes[1].travelButtonText=123);
invalid('travel label newline', c => c.sectors[0].nodes[1].travelButtonText='前往\n这里');
invalid('travel label control', c => c.sectors[0].nodes[1].travelButtonText='前往\u0085这里');
check('archive rich text valid and preserved', () => {
 for(const text of [undefined,null,'','  ','普通说明\n第二行','<b>粗体<i>斜体</i></b>','<color=#FFAA00><size=28>提示</size></color>','<color=#11223344>半透明</color>','字'.repeat(2000)]){
  const c=C.clone(source);if(text!==undefined)c.sectors[0].nodes[1].archiveDescription=text;
  assert.deepEqual(C.parse(JSON.stringify(c)),c);
 }
});
for(const [name,text] of Object.entries({unclosed:'<b>未关闭',crossed:'<b><i>错位</b></i>',html:'<img src=x>',large:'<size=99>大</size>',badColor:'<color=red>红</color>',tooLong:'字'.repeat(2001),wrongType:12})) invalid('archive '+name,c=>c.sectors[0].nodes[1].archiveDescription=text);
check('blocked engine setpiece accepts mode 10', () => { const c=C.clone(source);c.events[0].type='setpiece';c.events[0].setpiece=10;c.events[0].duration=8;assert.deepEqual(C.validate(c),[]); });
check('portal setpiece accepts mode 11', () => { const c=C.clone(source);c.events[0].type='setpiece';c.events[0].setpiece=11;c.events[0].duration=35;assert.deepEqual(C.validate(c),[]); });
check('mihuoer encounter accepts mode 12', () => { const c=C.clone(source);c.events[0].type='setpiece';c.events[0].setpiece=12;c.events[0].duration=1;assert.deepEqual(C.validate(c),[]); });
check('example round trip', () => assert.deepEqual(C.parse(JSON.stringify(source)), source));
invalid('version', c => c.schemaVersion = 2);
invalid('duplicate resources', c => c.resources[1].id = c.resources[0].id);
invalid('negative initial', c => c.resources[0].initial = -1);
invalid('fractional quantity', c => c.resources[0].initial = 1.5);
invalid('zero capacity', c => c.resources[0].capacity = 0);
invalid('missing cost array', c => c.travelCosts = null);
invalid('negative costs', c => c.travelCosts[0].amount = -2);
invalid('unknown resource', c => c.travelCosts[0].resourceId = 'bad');
invalid('duplicate costs', c => c.travelCosts.push(c.travelCosts[0]));
invalid('unknown handler', c => c.events[0].type = 'bad');
invalid('bad setpiece', c => c.events[0].setpiece = 13);
invalid('zero duration', c => c.events[0].duration = 0);
invalid('null event', c => c.events[0] = null);
invalid('null option', c => c.events[0].choices[0] = null);
invalid('duplicate options', c => c.events[0].choices[1].id = c.events[0].choices[0].id);
invalid('no free escape', c => c.events[0].choices[1].costs = [{resourceId:'fuel',amount:1}]);
invalid('unknown event', c => c.sectors[0].nodes[1].eventId = 'bad');
invalid('duplicate node', c => c.sectors[0].nodes[1].id = 'entry');
invalid('event on start', c => c.sectors[0].nodes[0].eventId = 'show_1');
invalid('dead end', c => c.sectors[0].nodes[1].next = []);
invalid('out of bounds', c => c.sectors[0].nodes[1].x = 2);
invalid('NaN coordinates', c => c.sectors[0].nodes[1].y = NaN);
invalid('dangling edge', c => c.sectors[0].nodes[1].next.push('missing'));
invalid('cycle', c => c.sectors[0].nodes[1].next.push('entry'));
invalid('duplicate edge', c => c.sectors[0].nodes[0].next.push('signal_a'));
invalid('unreachable node', c => c.sectors[0].nodes[0].next = ['signal_a']);
invalid('exit with edges', c => c.sectors[0].nodes[7].next = ['entry']);
invalid('missing exit', c => c.sectors[0].nodes[7].exit = false);
invalid('sector cycle', c => c.sectors[2].nextSectorId = 'sector_1');
invalid('unreachable sector', c => c.sectors[0].nextSectorId = '');
check('rename updates all incoming edges', () => { const c=C.clone(source), s=c.sectors[0]; C.renameNode(s,'salvage','cache'); assert.ok(s.nodes[1].next.includes('cache')); assert.ok(!C.validate(c).length); });
check('rename entry updates starting reference', () => { const c=C.clone(source), s=c.sectors[0]; C.renameNode(s,'entry','start'); assert.equal(s.startNodeId,'start'); assert.ok(!C.validate(c).length); });
check('rename duplicate does not mutate', () => { const c=C.clone(source), before=JSON.stringify(c); assert.throws(()=>C.renameNode(c.sectors[0],'entry','exit')); assert.equal(JSON.stringify(c),before); });
check('delete removes incoming edges', () => { const c=C.clone(source), s=c.sectors[0]; C.removeNode(s,'signal_a'); assert.ok(!s.nodes.some(n=>n.next.includes('signal_a'))); assert.ok(!C.validate(c).length); });
check('toggle edge is reversible', () => { const s=C.clone(source).sectors[0], before=JSON.stringify(s); C.toggleEdge(s,'entry','salvage'); C.toggleEdge(s,'entry','salvage'); assert.equal(JSON.stringify(s),before); });
check('self-link rejected',()=>assert.throws(()=>C.toggleEdge(source.sectors[0],'entry','entry')));
check('exit-link rejected',()=>assert.throws(()=>C.toggleEdge(source.sectors[0],'exit','entry')));
check('malformed JSON rejected',()=>assert.throws(()=>C.parse('{broken')));
check('oversized JSON rejected',()=>assert.throws(()=>C.parse(' '.repeat(2*1024*1024+1))));
invalid('meeting missing package ID', c => c.events.find(e=>e.type==='performance').performanceId='');
invalid('meeting invalid package path', c => c.events.find(e=>e.type==='performance').performanceId='../relay');
invalid('meeting multiple settlements', c => { const e=c.events.find(e=>e.type==='performance');e.choices.push({...e.choices[0],id:'second'}); });
invalid('meeting settlement has cost', c => c.events.find(e=>e.type==='performance').choices[0].costs=[{resourceId:'fuel',amount:1}]);
console.log(`RESULT PASS checks=${checks}`);


