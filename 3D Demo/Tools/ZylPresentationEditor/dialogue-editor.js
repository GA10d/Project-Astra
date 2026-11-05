'use strict';
const dialogueEditors=[];
function dialogueElement(tag,cls,text){const el=document.createElement(tag);if(cls)el.className=cls;if(text!==undefined)el.textContent=text;return el;}
function dialogueButton(text,action){const button=dialogueElement('button','',text);button.type='button';button.onclick=action;return button;}
function changeDialogue(action,key='freeDialogue'){const next=copy(values);action(next[key]);commit(next);}
function initDialogueEditor(){
 for(const spec of [
  {key:'freeDialogue',group:3,title:'米霍尔自由对话',help:'按顺序播放。时长结束后，无回复选项自动继续；有选项等待玩家选择，任意选项均继续下一段。等待回复不阻止巨兽靠近，达到疑惑距离后立即强制打断。',empty:'尚无自由对话。巨兽仍会靠近并触发疑惑与转场。'},
  {key:'stomachDialogue',group:7,title:'肚中会谈 · 对话内容',help:'转舱面向米霍尔后开始，保持舷窗视角。每段填写台词、持续时间和可选玩家回复。计时结束后，无选项自动继续；有选项等待玩家选择，选后进入下一段。最后一段结束后自动打开导航，不自动选择事件处置选项。',empty:'没有会谈段落：转舱完成后立即返回导航。'}
 ]){
  const group=$('group'+spec.group);const area=dialogueElement('section','dialogue-editor');
  const header=dialogueElement('div','group-title');header.append(dialogueElement('h3','',spec.title),dialogueButton('＋ 增加一段对话',()=>changeDialogue(lines=>lines.push({text:'请填写米霍尔的台词。',duration:5,replies:[]}),spec.key)));area.append(header);
  area.append(dialogueElement('p','dialogue-help',spec.help));spec.description=dialogueElement('p','dialogue-help');area.append(spec.description);
  spec.list=dialogueElement('div','dialogue-list');area.append(spec.list);group.append(area);dialogueEditors.push(spec);
 }
}
function renderDialogueBlocks(){for(const spec of dialogueEditors)renderDialogueSequence(spec);}
function renderDialogueSequence(spec){
 const key=spec.key,list=values[key],change=action=>changeDialogue(action,key);spec.list.replaceChildren();
 if(!Array.isArray(list)){spec.description.textContent='对话格式无效，请撤销或重新打开配置。';return;}
 spec.description.textContent=`当前 ${list.length} 段 · 可增删、复制、排序。台词建议20–200字，时长建议3–20秒；允许0.1–600秒。`;
 if(!list.length)spec.list.append(dialogueElement('p','dialogue-empty',spec.empty));
 list.forEach((line,i)=>{
  const card=dialogueElement('article','card dialogue-block');card.dataset.search=[spec.title,'玩家回复 台词 持续时间',line.text,...(line.replies||[])].join(' ').toLowerCase();
  const header=dialogueElement('div','dialogue-block-head');header.append(dialogueElement('strong','',`对话 ${String(i+1).padStart(2,'0')}`));const actions=dialogueElement('div','dialogue-actions');
  const up=dialogueButton('上移',()=>change(lines=>[lines[i-1],lines[i]]=[lines[i],lines[i-1]]));up.disabled=i===0;
  const down=dialogueButton('下移',()=>change(lines=>[lines[i+1],lines[i]]=[lines[i],lines[i+1]]));down.disabled=i===list.length-1;
  actions.append(up,down,dialogueButton('复制',()=>change(lines=>lines.splice(i+1,0,copy(lines[i])))),dialogueButton('删除本段',()=>change(lines=>lines.splice(i,1))));header.append(actions);card.append(header);
  const textLabel=dialogueElement('label','','米霍尔台词');textLabel.htmlFor=key+'-text-'+i;card.append(textLabel);
  const text=dialogueElement('textarea');Object.assign(text,{id:textLabel.htmlFor,rows:3,maxLength:1000,value:line.text,required:true});text.onchange=()=>change(lines=>lines[i].text=text.value);card.append(text);
  const timing=dialogueElement('div','dialogue-timing'),label=dialogueElement('label','','米霍尔台词持续时间');label.htmlFor=key+'-duration-'+i;timing.append(label);
  const duration=dialogueElement('input');Object.assign(duration,{id:label.htmlFor,type:'number',min:.1,max:600,step:'any',value:line.duration,required:true});duration.onchange=()=>change(lines=>lines[i].duration=duration.value.trim()===''?null:Number(duration.value));timing.append(duration,dialogueElement('span','','秒'));card.append(timing);
  const replyHead=dialogueElement('div','dialogue-block-head');replyHead.append(dialogueElement('strong','','玩家回复选项'),dialogueButton('＋ 增加玩家回复选项',()=>change(lines=>lines[i].replies.push('请填写玩家回复。'))));card.append(replyHead);
  if(!line.replies.length)card.append(dialogueElement('p','dialogue-help','没有回复选项：计时结束后自动进入下一段。'));
  line.replies.forEach((reply,j)=>{const row=dialogueElement('div','dialogue-reply'),label=dialogueElement('label','',`选项 ${j+1}`);label.htmlFor=`${key}-reply-${i}-${j}`;const input=dialogueElement('textarea');Object.assign(input,{id:label.htmlFor,rows:2,maxLength:160,value:reply,required:true});input.onchange=()=>change(lines=>lines[i].replies[j]=input.value);row.append(label,input,dialogueButton('删除',()=>change(lines=>lines[i].replies.splice(j,1))));card.append(row);});
  spec.list.append(card);
 });
}
