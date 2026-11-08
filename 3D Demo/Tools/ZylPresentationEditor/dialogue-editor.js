'use strict';
const dialogueEditors=[];
function dialogueElement(tag,cls,text){const el=document.createElement(tag);if(cls)el.className=cls;if(text!==undefined)el.textContent=text;return el;}
function dialogueButton(text,action){const button=dialogueElement('button','',text);button.type='button';button.onclick=action;return button;}
function changeDialogue(action,key,rerender=true){
 const next=copy(values);
 action(next[key]);
 commit(next,rerender);
}
let dialogueDrag=null;
function markDialogueDrop(card,after){
 if(dialogueDrag.marker===card&&dialogueDrag.after===after)return;
 clearDialogueDrop();
 dialogueDrag.marker=card;
 dialogueDrag.after=after;
 card.classList.add(after?'drop-after':'drop-before');
}
function clearDialogueDrop(){
 dialogueDrag?.marker?.classList.remove('drop-before','drop-after');
 if(dialogueDrag)dialogueDrag.marker=null;
}
function endDialogueDrag(){
 clearDialogueDrop();
 dialogueDrag?.card.classList.remove('dragging');
 dialogueDrag=null;
}
function moveDialogue(spec,from,boundary){
 const to=boundary>from?boundary-1:boundary;
 if(from===to||to<0||to>=values[spec.key].length)return;
 changeDialogue(lines=>{
  const [line]=lines.splice(from,1);
  lines.splice(to,0,line);
 },spec.key);
 spec.list.children[to].querySelector('.dialogue-drag-handle').focus({preventScroll:true});
}
function dialogueDropTarget(spec,event){
 const target=event.target.closest('.dialogue-block');
 if(target){
  const {top,height}=target.getBoundingClientRect();
  return [target,event.clientY>top+height/2];
 }
 const cards=[...spec.list.querySelectorAll('.dialogue-block:not([hidden])')];
 const next=cards.find(card=>event.clientY<card.getBoundingClientRect().top);
 return next?[next,false]:[cards.at(-1),true];
}
function bindDialogueDrop(spec){
 spec.list.addEventListener('dragover',event=>{
  if(dialogueDrag?.spec!==spec)return;
  event.preventDefault();
  event.dataTransfer.dropEffect='move';
  const [card,after]=dialogueDropTarget(spec,event);
  if(card)markDialogueDrop(card,after);
 });
 spec.list.addEventListener('dragleave',event=>{
  if(dialogueDrag?.spec===spec&&!spec.list.contains(event.relatedTarget))clearDialogueDrop();
 });
 spec.list.addEventListener('drop',event=>{
  if(dialogueDrag?.spec!==spec)return;
  event.preventDefault();
  const [card,after]=dialogueDropTarget(spec,event);
  const from=dialogueDrag.from;
  endDialogueDrag();
  if(card)moveDialogue(spec,from,Number(card.dataset.index)+Number(after));
 });
}
function dialogueHandle(spec,card,i,count){
 const handle=dialogueElement('button','dialogue-drag-handle','⠿ 拖动');
 handle.type='button';
 handle.draggable=count>1;
 handle.disabled=count<2;
 handle.title='拖到另一段的上方或下方；也可用方向键移动';
 handle.setAttribute('aria-label',`移动对话 ${i+1}，使用上、下方向键`);
 handle.addEventListener('dragstart',event=>{
  if(count<2){event.preventDefault();return;}
  dialogueDrag={spec,card,from:i,marker:null,after:false};
  event.dataTransfer.effectAllowed='move';
  event.dataTransfer.setData('text/plain',`对话 ${i+1}`);
  card.classList.add('dragging');
 });
 handle.addEventListener('dragend',endDialogueDrag);
 handle.addEventListener('keydown',event=>{
  if(event.key!=='ArrowUp'&&event.key!=='ArrowDown')return;
  event.preventDefault();
  moveDialogue(spec,i,event.key==='ArrowUp'?i-1:i+2);
 });
 return handle;
}
function setDialogueSearch(card,spec,line){
 card.dataset.search=[spec.title,'玩家回复 台词 持续时间',spec.key==='escapeGravesDialogue'?'肉芽 拔取 触发':'',line.text,...(line.replies||[])].join(' ').toLowerCase();
}
function initDialogueEditor(){
 for(const spec of [
  {key:'freeDialogue',group:3,title:'米霍尔自由对话',help:'按顺序播放。时长结束后，无回复选项自动继续；有选项等待玩家选择，任意选项均继续下一段。等待回复不阻止巨兽靠近，达到疑惑距离后立即强制打断。',empty:'尚无自由对话。巨兽仍会靠近并触发疑惑与转场。'},
  {key:'stomachDialogue',group:7,title:'肚中会谈 · 对话内容',help:'转舱面向米霍尔后开始，保持舷窗视角。每段填写台词、持续时间和可选玩家回复。计时结束后，无选项自动继续；有选项等待玩家选择，选后进入下一段。最后一段结束后自动打开导航，不自动选择事件处置选项。',empty:'没有会谈段落：转舱完成后立即返回导航。'},
  {key:'foolsGroundDialogue',group:10,cinematic:true,title:'离船后 · 台词',help:'菌群返回后，镜头切到已离船的米霍尔。对白依次播放，播完后开始上坡。',empty:'无离船对白：直接开始上坡。'},
  {key:'foolsWalkDialogue',group:10,cinematic:true,title:'上坡途中 · 台词',help:'边走边说。对白结束不停止行走；到坡顶立即打断当前段落并替换为坡顶新台词，不等待此处时长结束。',empty:'无上坡对白：静默行走，仍在坡顶触发新台词。'},
  {key:'foolsCrestDialogue',group:10,cinematic:true,title:'到达坡顶 · 打断台词',help:'到坡顶立即开始，镜头切正面、绕半圈到后脑并拉开。台词可贯穿镜头；最后一段播完且最终停留结束后返回导航。',empty:'无坡顶对白：只播放镜头并返回导航。'},
  {key:'farewellDialogue',group:12,title:'别离 · 米霍尔视频通话',help:'沿用航程现有倒计时，不自动重置。支持多段台词、玩家回复和每句/回复计时操作；归零优先打断任何台词及待选回复。台词播完也会等待归零。',empty:'无对白：保留实时画面，等待现有倒计时归零。'},
  {key:'escapeGravesDialogue',group:11,cinematic:true,title:'三座墓碑前 · 固定镜头对白',help:'勾选一段台词，米霍尔会在这句开始时弯腰拔肉芽。根须连着地面，拔出后等待设定的时间再晃动。对白和拔取演出结束后黑屏进入牵引逃亡。',empty:'无墓前对白：展示固定镜头后进入转场。'},
  {key:'escapeEncouragement',group:11,cinematic:true,title:'牵引逃亡 · 米霍尔鼓励台词',help:'战斗右侧通讯栏按段落时长播放，可自由增删。整段飞行以牵引逃亡时长为准；抵达出口会结束尚未播完的台词。',empty:'无飞行台词：右侧只显示船体和距离。'},
  {key:'foolsClosingDialogue',group:10,cinematic:true,title:'黑屏转场后 · 最后对白',help:'前段背影画面结束后先黑屏，再显现新站位。镜头固定，按顺序播放本组台词；全部结束后返回导航，仍手动结算傻瓜。',empty:'没有最后对白：新镜头显现后返回导航。'},
  {key:'printingDialogue',group:9,title:'米霍尔需要打印构件 · 对话内容',help:'切到舷窗，米霍尔坐在现有飞船上面向玩家。每段可配置台词、时长、回复与计时操作；最后一段结束后自动返回导航，不自动结算事件。',empty:'没有对话段落：切到舷窗并就座后立即返回导航。'}
 ]){
  const group=$('group'+spec.group);const area=dialogueElement('section','dialogue-editor');
  const header=dialogueElement('div','group-title');header.append(dialogueElement('h3','',spec.title));area.append(header);
  area.append(dialogueElement('p','dialogue-help',spec.help));spec.description=dialogueElement('p','dialogue-help');area.append(spec.description);
  spec.list=dialogueElement('div','dialogue-list');
  bindDialogueDrop(spec);
  const footer=dialogueElement('div','dialogue-footer');
  spec.add=dialogueButton('＋ 增加一段对话',()=>{
   changeDialogue(lines=>lines.push({text:'请填写米霍尔的台词。',duration:5,replies:[]}),spec.key);
   $(spec.key+'-text-'+(values[spec.key].length-1)).focus();
  });
  footer.append(spec.add);
  area.append(spec.list,footer);
  group.append(area);
  dialogueEditors.push(spec);
 }
}
function renderDialogueBlocks(){
 endDialogueDrag();
 for(const spec of dialogueEditors)renderDialogueSequence(spec);
 applySearch();
}
function renderDialogueSequence(spec){
 const key=spec.key,list=values[key],change=(action,rerender)=>changeDialogue(action,key,rerender);spec.list.replaceChildren();
 spec.add.disabled=!Array.isArray(list);
 if(!Array.isArray(list)){spec.description.textContent='对话格式无效，请撤销或重新打开配置。';return;}
 spec.description.textContent=`当前 ${list.length} 段 · 拖动卡片手柄调整顺序，也可用上移、下移。台词建议20–200字，时长建议3–20秒；允许0.1–600秒。`;
 if(!list.length)spec.list.append(dialogueElement('p','dialogue-empty',spec.empty));
 list.forEach((line,i)=>{
  const card=dialogueElement('article','card dialogue-block');card.dataset.index=i;setDialogueSearch(card,spec,line);
  const header=dialogueElement('div','dialogue-block-head');const heading=dialogueElement('div','dialogue-heading');heading.append(dialogueHandle(spec,card,i,list.length),dialogueElement('strong','',`对话 ${String(i+1).padStart(2,'0')}`));header.append(heading);const actions=dialogueElement('div','dialogue-actions');
  const up=dialogueButton('上移',()=>change(lines=>[lines[i-1],lines[i]]=[lines[i],lines[i-1]]));up.disabled=i===0;
  const down=dialogueButton('下移',()=>change(lines=>[lines[i+1],lines[i]]=[lines[i],lines[i+1]]));down.disabled=i===list.length-1;
  actions.append(up,down,dialogueButton('复制',()=>change(lines=>{const duplicate=ZYL_TIMER_DATA.copyLine(lines[i]);if(key==='escapeGravesDialogue')delete duplicate.pullSprout;lines.splice(i+1,0,duplicate);})),dialogueButton('删除本段',()=>change(lines=>lines.splice(i,1))));header.append(actions);card.append(header);
  const textLabel=dialogueElement('label','','米霍尔台词');textLabel.htmlFor=key+'-text-'+i;card.append(textLabel);
  const text=dialogueElement('textarea');Object.assign(text,{id:textLabel.htmlFor,rows:3,maxLength:1000,value:line.text,required:true});text.onchange=()=>{change(lines=>lines[i].text=text.value,false);setDialogueSearch(card,spec,values[key][i]);};card.append(text);
  const timing=dialogueElement('div','dialogue-timing'),label=dialogueElement('label','','米霍尔台词持续时间');label.htmlFor=key+'-duration-'+i;timing.append(label);
  const duration=dialogueElement('input');Object.assign(duration,{id:label.htmlFor,type:'number',min:.1,max:600,step:'any',value:line.duration,required:true});duration.onchange=()=>change(lines=>lines[i].duration=duration.value.trim()===''?null:Number(duration.value),false);timing.append(duration,dialogueElement('span','','秒'));card.append(timing);renderTimerAction(card,line.timerAction,action=>change(lines=>{ZYL_TIMER_DATA.normalizeLine(lines[i]);lines[i].timerAction=action;}));
  if(key==='escapeGravesDialogue'){
   const cue=dialogueElement('label','dialogue-timing');
   const checkbox=dialogueElement('input');Object.assign(checkbox,{type:'checkbox',checked:line.pullSprout===true});
   checkbox.onchange=()=>change(lines=>{
    if(checkbox.checked)lines.forEach(row=>delete row.pullSprout);
    if(checkbox.checked)lines[i].pullSprout=true;else delete lines[i].pullSprout;
   });
   cue.append(checkbox,dialogueElement('span','','在这句开始拔肉芽'));card.append(cue);
  }
  if(spec.cinematic){card.append(dialogueElement('p','dialogue-help','此处为连续演出对白，不设置玩家回复。'));spec.list.append(card);return;}
  const replyHead=dialogueElement('div','dialogue-block-head');replyHead.append(dialogueElement('strong','','玩家回复选项'),dialogueButton('＋ 增加玩家回复选项',()=>change(lines=>{const row=lines[i];ZYL_TIMER_DATA.normalizeLine(row);row.replies.push('请填写玩家回复。');row.replyTimerActions.push(null);})));card.append(replyHead);
  if(!line.replies.length)card.append(dialogueElement('p','dialogue-help','没有回复选项：计时结束后自动进入下一段。'));
  line.replies.forEach((reply,j)=>{const row=dialogueElement('div','dialogue-reply'),label=dialogueElement('label','',`选项 ${j+1}`);label.htmlFor=`${key}-reply-${i}-${j}`;const input=dialogueElement('textarea');Object.assign(input,{id:label.htmlFor,rows:2,maxLength:160,value:reply,required:true});input.onchange=()=>{change(lines=>lines[i].replies[j]=input.value,false);setDialogueSearch(card,spec,values[key][i]);};row.append(label,input,dialogueButton('删除',()=>change(lines=>{ZYL_TIMER_DATA.normalizeLine(lines[i]);lines[i].replies.splice(j,1);lines[i].replyTimerActions.splice(j,1);})));card.append(row);renderTimerAction(card,line.replyTimerActions?.[j],action=>change(lines=>{ZYL_TIMER_DATA.normalizeLine(lines[i]);lines[i].replyTimerActions[j]=action;}));});
  spec.list.append(card);
 });
}
