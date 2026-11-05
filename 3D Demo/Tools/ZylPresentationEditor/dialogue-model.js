'use strict';
// Pure data migration/validation shared by import, drafts and the repeatable editor.
window.ZYL_DIALOGUE={
 normalize(raw,defaults){
  const next=JSON.parse(JSON.stringify({...defaults,...raw}));
  if(raw.freeDialogue==null){
   const old=Object.hasOwn(raw,'explainText')||Object.hasOwn(raw,'thinkText')||Object.hasOwn(raw,'explainDuration');
   next.freeDialogue=old?[
    {text:raw.explainText??defaults.freeDialogue[0]?.text??'我想……',duration:raw.explainDuration??10,replies:[]},
    {text:raw.thinkText??defaults.freeDialogue[1]?.text??'我需要想想……',duration:600,replies:[]}
   ]:JSON.parse(JSON.stringify(defaults.freeDialogue));
  }
  if(Array.isArray(next.freeDialogue))for(const line of next.freeDialogue)if(line&&typeof line==='object'&&line.replies==null)line.replies=[];
  if(Array.isArray(next.stomachDialogue))for(const line of next.stomachDialogue)if(line&&typeof line==='object'&&line.replies==null)line.replies=[];
  for(const key of ['freeDialogue','stomachDialogue'])if(Array.isArray(next[key]))next[key].forEach(line=>ZYL_TIMER_DATA.normalizeLine(line));
  for(const key of ['explainText','thinkText','explainDuration'])delete next[key];
  return next;
 },
 validate(lines){
  const errors=[],validText=(s,max)=>typeof s==='string'&&s.trim()&&s.length<=max&&!/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F]/.test(s);
  if(!Array.isArray(lines))return ['自由对话必须为段落数组。'];
  lines.forEach((line,i)=>{
   if(!line||typeof line!=='object'||Array.isArray(line)){errors.push(`自由对话 ${i+1}：段落格式无效。`);return;}
   if(!validText(line.text,1000))errors.push(`自由对话 ${i+1}：米霍尔台词需1–1000字符，支持换行。`);
   if(typeof line.duration!=='number'||!Number.isFinite(line.duration)||line.duration<.1||line.duration>600)errors.push(`自由对话 ${i+1}：持续时间需0.1–600秒。`);
   if(line.timerKey!=null&&(typeof line.timerKey!=="string"||!/^[A-Za-z0-9_-]{1,48}$/.test(line.timerKey)))errors.push(`自由对话 ${i+1}：段落计时标识无效。`);const error=ZYL_TIMER_DATA.validate(line.timerAction);if(error)errors.push(`自由对话 ${i+1}：${error}`);
   if(line.replyTimerActions!=null){if(!Array.isArray(line.replyTimerActions)||!Array.isArray(line.replies)||line.replyTimerActions.length!==line.replies.length)errors.push(`自由对话 ${i+1}：回复计时操作必须与选项一一对应。`);else line.replyTimerActions.forEach((a,j)=>{let error=ZYL_TIMER_DATA.validate(a);if(error)errors.push(`自由对话 ${i+1} 回复 ${j+1}：${error}`);});}
   if(!Array.isArray(line.replies)){errors.push(`自由对话 ${i+1}：玩家回复必须为数组。`);return;}
   line.replies.forEach((reply,j)=>{if(!validText(reply,160))errors.push(`自由对话 ${i+1} 回复 ${j+1}：需1–160字符。`);});
  });return errors;
 }
};
