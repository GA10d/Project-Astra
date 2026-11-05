'use strict';
let dialogueList,dialogueDescription;
function dialogueElement(tag,cls,text){const el=document.createElement(tag);if(cls)el.className=cls;if(text!==undefined)el.textContent=text;return el;}
function dialogueButton(text,action){const button=dialogueElement('button','',text);button.type='button';button.onclick=action;return button;}
function changeDialogue(action){const next=copy(values);action(next.freeDialogue);commit(next);}
function initDialogueEditor(){
 const group=$('group3');const area=dialogueElement('section','dialogue-editor');
 const header=dialogueElement('div','group-title');header.append(dialogueElement('h3','','米霍尔自由对话'),dialogueButton('＋ 增加一段对话',()=>changeDialogue(lines=>lines.push({text:'请填写米霍尔的台词。',duration:5,replies:[]}))));area.append(header);
 area.append(dialogueElement('p','dialogue-help','按顺序播放。每段持续时间结束后，无回复选项则自动继续；有选项则等待玩家选择，任意选项均继续下一段。等待回复不会阻止巨兽靠近。达到疑惑距离时，立即取消当前台词与选项，切入已配置的“远处疑惑”。所有段落结束后不再显示自由台词，巨兽继续靠近。'));
 dialogueDescription=dialogueElement('p','dialogue-help');area.append(dialogueDescription);
 dialogueList=dialogueElement('div','dialogue-list');area.append(dialogueList);group.append(area);
}
function renderDialogueBlocks(){
 if(!dialogueList)return;dialogueList.replaceChildren();
 if(!Array.isArray(values.freeDialogue)){dialogueDescription.textContent='自由对话格式无效，请撤销或重新打开配置。';return;}
 dialogueDescription.textContent=`当前 ${values.freeDialogue.length} 段 · 可增删、复制、调整顺序。台词建议20–200字，时长建议3–20秒；允许0.1–600秒。保存前会检查空台词和空回复。`;
 if(!values.freeDialogue.length)dialogueList.append(dialogueElement('p','dialogue-empty','尚无自由对话。初始问候与回复后，巨兽仍会靠近并触发疑惑与转场。'));
 values.freeDialogue.forEach((line,i)=>{
  const card=dialogueElement('article','card dialogue-block');card.dataset.search=['米霍尔 自由对话 玩家回复 台词 持续时间',line.text,...(line.replies||[])].join(' ').toLowerCase();
  const header=dialogueElement('div','dialogue-block-head');header.append(dialogueElement('strong','',`对话 ${String(i+1).padStart(2,'0')}`));
  const actions=dialogueElement('div','dialogue-actions');
  const up=dialogueButton('上移',()=>changeDialogue(lines=>[lines[i-1],lines[i]]=[lines[i],lines[i-1]]));up.disabled=i===0;
  const down=dialogueButton('下移',()=>changeDialogue(lines=>[lines[i+1],lines[i]]=[lines[i],lines[i+1]]));down.disabled=i===values.freeDialogue.length-1;
  actions.append(up,down,dialogueButton('复制',()=>changeDialogue(lines=>lines.splice(i+1,0,copy(lines[i])))),dialogueButton('删除本段',()=>changeDialogue(lines=>lines.splice(i,1))));header.append(actions);card.append(header);
  const textLabel=dialogueElement('label','','米霍尔台词');textLabel.htmlFor='dialogue-text-'+i;card.append(textLabel);
  const text=dialogueElement('textarea');Object.assign(text,{id:'dialogue-text-'+i,rows:3,maxLength:1000,value:line.text,required:true});text.onchange=()=>changeDialogue(lines=>lines[i].text=text.value);card.append(text);
  const timing=dialogueElement('div','dialogue-timing');const label=dialogueElement('label','','米霍尔台词持续时间');label.htmlFor='dialogue-duration-'+i;timing.append(label);
  const duration=dialogueElement('input');Object.assign(duration,{id:'dialogue-duration-'+i,type:'number',min:.1,max:600,step:'any',value:line.duration,required:true});duration.onchange=()=>changeDialogue(lines=>lines[i].duration=duration.value.trim()===''?null:Number(duration.value));timing.append(duration,dialogueElement('span','','秒'));card.append(timing);
  const replyHead=dialogueElement('div','dialogue-block-head');replyHead.append(dialogueElement('strong','','玩家回复选项'),dialogueButton('＋ 增加玩家回复选项',()=>changeDialogue(lines=>lines[i].replies.push('请填写玩家回复。'))));card.append(replyHead);
  if(!line.replies.length)card.append(dialogueElement('p','dialogue-help','没有回复选项：台词计时结束后自动进入下一段。'));
  line.replies.forEach((reply,j)=>{const row=dialogueElement('div','dialogue-reply');const label=dialogueElement('label','',`选项 ${j+1}`);label.htmlFor=`dialogue-reply-${i}-${j}`;const input=dialogueElement('textarea');Object.assign(input,{id:label.htmlFor,rows:2,maxLength:160,value:reply,required:true});input.onchange=()=>changeDialogue(lines=>lines[i].replies[j]=input.value);row.append(label,input,dialogueButton('删除',()=>changeDialogue(lines=>lines[i].replies.splice(j,1))));card.append(row);});
  dialogueList.append(card);
 });
}
