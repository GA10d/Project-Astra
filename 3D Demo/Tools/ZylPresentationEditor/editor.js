'use strict';
const {groups,fields,defaults,rules}=window.ZYL_PARAMETERS;
const $=id=>document.getElementById(id), legacyDraftKey='astra-zyl-presentation-v1';
const pageKey=location.pathname, draftKey=legacyDraftKey+':'+pageKey;
let values=ZYL_DIALOGUE.normalize(defaults,defaults),history=[],future=[],handle=null,baseline=null,revision=0,savedRevision=0,filename='未关联文件',busy=false;
let rememberedFile=null,fileId=null,fileConflict=false,cachedFile=false;
const copy=v=>JSON.parse(JSON.stringify(v));
function status(message){
 const state=revision===savedRevision?(cachedFile&&!handle?'浏览器副本':'已保存 / 默认值'):'有未保存修改';
 const association=fileConflict?' · 与磁盘版本冲突，直接保存已阻止':
     handle?' · 已关联文件':rememberedFile?' · 请重新连接上次文件':' · 请打开配置后保存，或下载 JSON';
 $('status').textContent=message||`${filename} · ${state}${association}`;
 $('reconnect').hidden=!rememberedFile||!!handle;
}
function validate(v){const errors=ZYL_DIALOGUE.validate(v.farewellDialogue).concat(ZYL_DIALOGUE.validate(v.freeDialogue)).concat(ZYL_DIALOGUE.validate(v.stomachDialogue).map(e=>e.replaceAll("自由对话","肚中会谈")),ZYL_DIALOGUE.validate(v.printingDialogue).map(e=>e.replaceAll("自由对话","打印构件对白")));for(const key of ['foolsGroundDialogue','foolsWalkDialogue','foolsCrestDialogue','foolsClosingDialogue','escapeGravesDialogue','escapeEncouragement']){errors.push(...ZYL_DIALOGUE.validate(v[key]).map(e=>e.replaceAll('自由对话',key)));if(Array.isArray(v[key])&&v[key].some(line=>line?.replies?.length))errors.push('连续演出对白不支持玩家回复。');}if(Array.isArray(v.escapeGravesDialogue)){const lines=v.escapeGravesDialogue;if(lines.some(line=>line&&Object.hasOwn(line,'pullSprout')&&typeof line.pullSprout!=='boolean'))errors.push('墓前对白的拔肉芽勾选必须是布尔值。');if(lines.filter(line=>line?.pullSprout===true).length>1)errors.push('只能勾选一句台词开始拔肉芽。');}if(v.schemaVersion!==1)errors.push('只支持 schemaVersion 1。');for(const f of fields){let n=v[f.key];if(f.type==='color'){if(typeof n!=='string'||!/^#[0-9a-fA-F]{6}$/.test(n))errors.push(`${f.label}：需要#RRGGBB颜色。`);continue;}if(f.type==='text'){if(typeof n!=='string'||!n.trim()||n.length>f.maxLength||/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F]/.test(n))errors.push(`${f.label}：请填写1–${f.maxLength}个字符的台词，支持换行。`);continue;}if(typeof n!=='number'||!Number.isFinite(n)||n<f.min||n>f.max)errors.push(`${f.label}：请输入 ${f.min}–${f.max} 之间的数值。`);}if(!Number.isInteger(v.starCount))errors.push('通话星点数量必须是整数。');for(const [a,b]of rules)if(v[a]>=v[b])errors.push(`${fields.find(f=>f.key===a).label}必须小于${fields.find(f=>f.key===b).label}。`);return errors;}
function persist(){
 try{
  const dirty=revision!==savedRevision;
  localStorage.setItem(draftKey,JSON.stringify({values,dirty,fileId,baseline,filename,cachedFile}));
 }catch{status('浏览器草稿不可用，请及时保存或下载。');}
}
function commit(next,renderDialogue=true){history.push(copy(values));if(history.length>100)history.shift();future=[];values=next;revision++;persist();refresh();if(renderDialogue)renderDialogueBlocks();}
function refresh(){for(const f of fields){const input=$(f.key);if(document.activeElement!==input)input.value=f.type==='time'?ZYL_TIMER_DATA.format(values[f.key]):['text','color'].includes(f.type)?(values[f.key]??''):(Number.isFinite(values[f.key])?values[f.key]:'');input.closest('.card').classList.toggle('changed',values[f.key]!==f.value);}$('undo').disabled=!history.length;$('redo').disabled=!future.length;$('errors').textContent=validate(values).join('\n');$('summary').replaceChildren(...[['巨兽初始口部距离',values.initialClearance,'单位'],['全黑加载触发距离',values.loadDistance,'单位'],['胃内淡入时长',values.fadeInDuration,'秒']].map(([s,n,u])=>{let d=document.createElement('div');d.className='metric';d.textContent=s;let b=document.createElement('b');b.textContent=Number.isFinite(n)?n:'—';let small=document.createElement('small');small.textContent=u;b.append(small);d.append(b);return d;}));status();}
groups.forEach((g,i)=>{let link=document.createElement('a');link.href='#group'+i;link.textContent=g;$('nav').append(link);let section=document.createElement('section');section.className='group';section.id='group'+i;let heading=document.createElement('div');heading.className='group-title';let h=document.createElement('h3');h.textContent=g;let reset=document.createElement('button');reset.textContent='恢复本组默认值';reset.onclick=()=>{const next=copy(values);fields.filter(f=>f.group===g).forEach(f=>next[f.key]=f.value);if(i===3)next.freeDialogue=copy(defaults.freeDialogue);if(i===7)next.stomachDialogue=copy(defaults.stomachDialogue);if(i===9)next.printingDialogue=copy(defaults.printingDialogue);if(i===10)for(const key of ['foolsGroundDialogue','foolsWalkDialogue','foolsCrestDialogue','foolsClosingDialogue'])next[key]=copy(defaults[key]);if(i===12)next.farewellDialogue=copy(defaults.farewellDialogue);if(i===11)for(const key of ['escapeGravesDialogue','escapeEncouragement'])next[key]=copy(defaults[key]);commit(next);};heading.append(h,reset);section.append(heading);let grid=document.createElement('div');grid.className='grid';fields.filter(f=>f.group===g).forEach(f=>{let card=document.createElement('article');card.className='card';card.dataset.search=[f.label,f.desc,f.key].join(' ').toLowerCase();card.innerHTML='<div class="field-top"><label></label><span class="tag"></span></div><div class="id"></div><div class="desc"></div><div class="value-row"><input type="number"><span></span><button type="button">还原</button></div><div class="range"></div>';let label=card.querySelector('label');label.textContent=f.label;label.htmlFor=f.key;card.querySelector('.tag').textContent=f.unit;card.querySelector('.id').textContent=f.key;card.querySelector('.desc').textContent=f.desc;if(f.type==='text'){card.classList.add('text-card');card.querySelector('.value-row').innerHTML='<textarea rows=4></textarea><span></span><button type=button>还原</button>';}let input=card.querySelector(f.type==='text'?'textarea':'input');if(f.type==='text')Object.assign(input,{id:f.key,maxLength:f.maxLength,required:true});else if(f.type==='color')Object.assign(input,{id:f.key,type:'color',value:f.value,required:true});else if(f.type==='time')Object.assign(input,{id:f.key,type:'text',placeholder:'00:05:00',required:true});else Object.assign(input,{id:f.key,min:f.min,max:f.max,step:f.key==='starCount'?'1':'any',required:true});input.setAttribute('aria-describedby',f.key+'hint');input.addEventListener('change',()=>{let next=copy(values);next[f.key]=f.type==='time'?ZYL_TIMER_DATA.parse(input.value):['text','color'].includes(f.type)?input.value:(input.value.trim()===''?null:Number(input.value));commit(next);});card.querySelector('.value-row span').textContent=f.unit;card.querySelector('button').onclick=()=>{let next=copy(values);next[f.key]=f.value;commit(next);};let hint=card.querySelector('.range');hint.id=f.key+'hint';hint.textContent=f.type==='time'?`允许 ${ZYL_TIMER_DATA.format(f.min)}–${ZYL_TIMER_DATA.format(f.max)} · 默认 ${ZYL_TIMER_DATA.format(f.value)}`:f.type==='color'?`默认 ${f.value}`:f.type==='text'?`最多 ${f.maxLength} 字符 · 可换行 · 点击“还原”恢复原台词`:`建议及允许范围 ${f.min}–${f.max} · 默认 ${f.value}`;grid.append(card);});section.append(grid);$('sections').append(section);});
$('count').textContent=fields.length+' 个固定参数 + 自由对话段落';
initDialogueEditor();renderDialogueBlocks();
function applySearch(){let q=$('search').value.toLowerCase();document.querySelectorAll('.card').forEach(el=>el.hidden=!el.dataset.search.includes(q));document.querySelectorAll('.group').forEach(el=>el.hidden=!!q&&!el.textContent.toLowerCase().includes(q)&&!Array.from(el.querySelectorAll('.card')).some(c=>!c.hidden));}
$('search').oninput=applySearch;
$('undo').onclick=()=>{if(!history.length)return;future.push(copy(values));values=history.pop();revision++;persist();refresh();renderDialogueBlocks();};
$('redo').onclick=()=>{if(!future.length)return;history.push(copy(values));values=future.pop();revision++;persist();refresh();renderDialogueBlocks();};
function parseConfig(text){
 const raw=JSON.parse(text);
 if(!raw||Array.isArray(raw)||typeof raw!=='object')throw Error('需要 JSON 对象');
 if(raw.schemaVersion!==1)throw Error('不支持的参数版本');
 const next=ZYL_DIALOGUE.normalize(raw,defaults), errors=validate(next);
 if(errors.length)throw Error(errors.join('\n'));
 return next;
}

function recentFileRequest(mode,request){
 return new Promise((resolve,reject)=>{
  const open=indexedDB.open('astra-zyl-presentation-files',1);
  open.onupgradeneeded=()=>open.result.createObjectStore('recent');
  open.onerror=()=>reject(open.error);
  open.onsuccess=()=>{
   const database=open.result, transaction=database.transaction('recent',mode);
   transaction.onabort=transaction.onerror=()=>{database.close();reject(transaction.error);};
   let operation;
   try{operation=request(transaction.objectStore('recent'));}
   catch(e){database.close();reject(e);return;}
   transaction.oncomplete=()=>{database.close();resolve(operation.result);};
  };
 });
}

async function rememberFile(){
 rememberedFile=handle?{handle,id:fileId}:null;
 try{
  await recentFileRequest('readwrite',store=>rememberedFile?store.put(rememberedFile,pageKey):store.delete(pageKey));
  return true;
 }catch{return false;}
}

async function importText(text,name,newHandle){
 const next=parseConfig(text);
 handle=newHandle;
 fileId=handle?ZYL_TIMER_DATA.key():null;
 baseline=handle?text:null;
 filename=name;
 cachedFile=true;
 fileConflict=false;
 commit(next);
 savedRevision=revision;
 const remembered=await rememberFile();
 persist();
 refresh();
 if(handle&&!remembered)status('已打开 '+name+'，浏览器无法记住这个文件。刷新后请重新打开配置。');
}

function restoreDraft(){
 try{
  const stored=localStorage.getItem(draftKey);
  if(stored){
   const draft=JSON.parse(stored);
   if(draft.values?.schemaVersion!==1)throw Error('不支持的草稿格式');
   values=ZYL_DIALOGUE.normalize(draft.values,defaults);
   fileId=draft.fileId??null;
   baseline=draft.baseline??null;
   filename=draft.filename||filename;
   cachedFile=!!draft.cachedFile;
   if(draft.dirty)revision=1;
   refresh();
   renderDialogueBlocks();
   return true;
  }
  const legacy=localStorage.getItem(legacyDraftKey);
  if(legacy){
   values=parseConfig(legacy);
   revision=1;
   refresh();
   renderDialogueBlocks();
   status('已恢复旧版浏览器草稿。请打开配置建立文件关联，或另存为。');
  }
 }catch{status('无法恢复浏览器草稿；请重新打开配置。');}
 return false;
}

async function reopenRemembered(){
 const file=await rememberedFile.handle.getFile(), text=await file.text(), next=parseConfig(text);
 const dirty=revision!==savedRevision;
 handle=rememberedFile.handle;
 filename=file.name;
 cachedFile=true;
 fileConflict=dirty&&(fileId!==rememberedFile.id||baseline!==text);
 if(!dirty){
  values=next;
  history=[];
  future=[];
  fileId=rememberedFile.id;
  baseline=text;
 }
 persist();
 refresh();
 renderDialogueBlocks();
 if(!dirty)status('已从磁盘重新读取 '+filename+'。');
}

async function restoreLastFile(hadScopedDraft){
 try{
  const record=await recentFileRequest('readonly',store=>store.get(pageKey));
  if(!record?.handle||hadScopedDraft&&record.id!==fileId)return;
  rememberedFile=record;
  if(await record.handle.queryPermission({mode:'read'})==='granted')await reopenRemembered();
  else status('请点击「重新连接上次文件」授权读取 '+record.handle.name+'。');
 }catch{
  if(rememberedFile)status('无法读取上次的文件。已保留页面内容，请重新连接或打开配置。');
 }
}

$('reconnect').onclick=()=>guarded(async()=>{
 if(!rememberedFile)return;
 if(await rememberedFile.handle.requestPermission({mode:'read'})!=='granted')
  throw Error('未取得读取权限，请重新连接或打开配置。');
 await reopenRemembered();
});
async function guarded(action){if(busy)return;busy=true;for(const id of ['open','reconnect','save','saveas','export'])$(id).disabled=true;try{await action();}catch(e){if(e.name!=='AbortError')status('操作失败：'+e.message);}finally{busy=false;for(const id of ['open','reconnect','save','saveas','export'])$(id).disabled=false;}}
$('open').onclick=()=>guarded(async()=>{if(revision!==savedRevision&&!confirm('当前有未保存修改，仍要打开其他配置？草稿将被替换。'))return;if(window.showOpenFilePicker){let [selected]=await showOpenFilePicker({types:[{description:'ZYL 演出参数',accept:{'application/json':['.json']}}],multiple:false});let file=await selected.getFile();await importText(await file.text(),file.name,selected);}else $('file').click();});
$('file').onchange=()=>guarded(async()=>{let f=$('file').files[0];if(f)await importText(await f.text(),f.name,null);$('file').value='';});
function serialized(){if(document.activeElement && typeof document.activeElement.blur === 'function')document.activeElement.blur();let errors=validate(values);if(errors.length)throw Error('请先修正参数：\n'+errors.join('\n'));return JSON.stringify(values,null,2)+'\n';}
function download(){let text=serialized(),url=URL.createObjectURL(new Blob([text],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='presentation.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),10000);status('已生成下载；请确认浏览器下载完成并将 presentation.json 放回源码目录。');}
async function save(as){
 const text=serialized(),atRevision=revision;
 if(!window.showSaveFilePicker){download();return;}
 if(!as&&!handle&&rememberedFile)throw Error('请先重新连接上次文件，或使用另存为。');
 let selected=handle;
 if(as||!selected)selected=await showSaveFilePicker({suggestedName:'presentation.json',types:[{description:'ZYL 演出参数',accept:{'application/json':['.json']}}]});
 const previous=handle||rememberedFile?.handle;
 const same=!!previous&&(selected===previous||await selected.isSameEntry(previous));
 if(same&&(fileConflict||baseline===null||(!handle&&fileId!==rememberedFile.id)))
  throw Error('文件与草稿的原始版本不一致。请重新打开配置，或另存为其他文件。');
 if(!as&&selected===handle&&await selected.requestPermission({mode:'readwrite'})!=='granted')
  throw Error('未取得写入权限。');
 if(same&&await (await selected.getFile()).text()!==baseline){
  fileConflict=true;
  throw Error('文件已被外部修改，已阻止覆盖。请重新打开配置，或另存为其他文件。');
 }
 const writable=await selected.createWritable();
 try{await writable.write(text);await writable.close();}
 catch(e){await writable.abort();throw e;}
 handle=selected;
 baseline=text;
 filename=selected.name;
 cachedFile=true;
 fileConflict=false;
 savedRevision=atRevision;
 if(!same)fileId=ZYL_TIMER_DATA.key();
 const remembered=await rememberFile();
 persist();
 status(remembered?null:'已保存，浏览器无法记住这个文件。刷新后请重新打开配置。');
}
$('save').onclick=()=>guarded(()=>save(false));$('saveas').onclick=()=>guarded(()=>save(true));$('export').onclick=()=>guarded(async()=>download());
window.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='s'){e.preventDefault();guarded(()=>save(e.shiftKey));}});
window.addEventListener('beforeunload',e=>{
 document.activeElement?.blur();
 if(revision!==savedRevision){e.preventDefault();e.returnValue='';}
});
refresh();
guarded(async()=>{
 $('sections').inert=true;
 try{
  const hadScopedDraft=restoreDraft();
  await restoreLastFile(hadScopedDraft);
 }finally{$('sections').inert=false;}
});
