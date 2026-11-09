'use strict';
window.ZYL_TIMER_DATA={
 maxSeconds:359999,
 key(){return 'timer_'+Date.now().toString(36)+'_'+Math.random().toString(36).slice(2,12);},
 copyLine(line){const next=JSON.parse(JSON.stringify(line));next.timerKey=this.key();return next;},
 operations:[['none','不操作'],['set','设置剩余时间'],['setStart','设置并开始'],['start','开始'],['pause','暂停'],['resume','继续'],['add','增加时间'],['subtract','减少时间'],['stopHide','停止并隐藏'],['show','显示'],['hide','隐藏'],['freezeDisplay','冻结显示为指定时间'],['unfreezeDisplay','解除显示冻结']],
 needsTime(kind){return ['set','setStart','add','subtract','freezeDisplay'].includes(kind);},
 format(seconds){if(!Number.isFinite(seconds))return '';const n=Math.ceil(Math.max(0,Math.min(this.maxSeconds,seconds)));return [Math.floor(n/3600),Math.floor(n/60)%60,n%60].map(part=>String(part).padStart(2,'0')).join(':');},
 parse(text){
  const value=String(text).trim();
  let seconds;
  if(/^\d{1,2}:[0-5]\d:[0-5]\d$/.test(value)){const [h,m,s]=value.split(':').map(Number);seconds=h*3600+m*60+s;}
  else if(/^\d{1,2}:[0-5]\d$/.test(value)){const [m,s]=value.split(':').map(Number);seconds=m*60+s;}
  else if(/^\d+(?:\.\d+)?$/.test(value))seconds=Number(value);
  return Number.isFinite(seconds)&&seconds<=this.maxSeconds?seconds:null;
 },
 validate(action){if(action==null)return null;if(typeof action!=='object'||!this.operations.some(o=>o[0]===action.kind))return '未知倒计时操作';if(this.needsTime(action.kind)&&(typeof action.seconds!=='number'||!Number.isFinite(action.seconds)||action.seconds<0||action.seconds>this.maxSeconds))return '操作时间需为00:00:00–99:59:59';return null;},
 normalizeLine(line){if(!line||typeof line!=='object')return;if(!line.timerKey)line.timerKey=this.key();if(line.timerAction===undefined)line.timerAction=null;if(line.replyTimerActions==null)line.replyTimerActions=Array.from({length:Array.isArray(line.replies)?line.replies.length:0},()=>null);}
};
function renderTimerAction(parent,action,onChange){
 const current=action||{kind:'none',seconds:0},area=dialogueElement('div','timer-action');
 const label=dialogueElement('span','','倒计时操作'),select=dialogueElement('select');select.setAttribute('aria-label','倒计时操作');
 for(const [value,title]of ZYL_TIMER_DATA.operations){const option=dialogueElement('option','',title);option.value=value;select.append(option);}select.value=current.kind;
 select.onchange=()=>onChange({kind:select.value,seconds:current.seconds??0});area.append(label,select);
 if(ZYL_TIMER_DATA.needsTime(current.kind)){
  const input=dialogueElement('input');input.type='text';input.value=ZYL_TIMER_DATA.format(current.seconds);input.placeholder='00:05:00';input.setAttribute('aria-label','倒计时操作时间');input.onchange=()=>onChange({kind:current.kind,seconds:ZYL_TIMER_DATA.parse(input.value)});area.append(input,dialogueElement('span','','时:分:秒，也可填分:秒或总秒数'));
 }
 parent.append(area);
}
