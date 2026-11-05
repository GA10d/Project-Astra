'use strict';
window.ZYL_TIMER_DATA={
 key(){return 'timer_'+Date.now().toString(36)+'_'+Math.random().toString(36).slice(2,12);},
 copyLine(line){const next=JSON.parse(JSON.stringify(line));next.timerKey=this.key();return next;},
 operations:[['none','不操作'],['set','设置剩余时间'],['setStart','设置并开始'],['start','开始'],['pause','暂停'],['resume','继续'],['add','增加时间'],['subtract','减少时间'],['stopHide','停止并隐藏'],['show','显示'],['hide','隐藏'],['freezeDisplay','冻结显示为指定时间'],['unfreezeDisplay','解除显示冻结']],
 needsTime(kind){return ['set','setStart','add','subtract','freezeDisplay'].includes(kind);},
 format(seconds){if(!Number.isFinite(seconds))return '';const n=Math.ceil(Math.max(0,Math.min(5999,seconds)));return String(Math.floor(n/60)).padStart(2,'0')+':'+String(n%60).padStart(2,'0');},
 parse(text){const value=String(text).trim();if(/^\d{1,2}:[0-5]\d$/.test(value)){let [m,s]=value.split(':').map(Number);return m*60+s;}if(/^\d+(?:\.\d+)?$/.test(value)){const n=Number(value);return n<=5999?n:null;}return null;},
 validate(action){if(action==null)return null;if(typeof action!=='object'||!this.operations.some(o=>o[0]===action.kind))return '未知倒计时操作';if(this.needsTime(action.kind)&&(typeof action.seconds!=='number'||!Number.isFinite(action.seconds)||action.seconds<0||action.seconds>5999))return '操作时间需为0–99:59';return null;},
 normalizeLine(line){if(!line||typeof line!=='object')return;if(!line.timerKey)line.timerKey=this.key();if(line.timerAction===undefined)line.timerAction=null;if(line.replyTimerActions==null)line.replyTimerActions=Array.from({length:Array.isArray(line.replies)?line.replies.length:0},()=>null);}
};
function renderTimerAction(parent,action,onChange){
 const current=action||{kind:'none',seconds:0},area=dialogueElement('div','timer-action');
 const label=dialogueElement('span','','倒计时操作'),select=dialogueElement('select');select.setAttribute('aria-label','倒计时操作');
 for(const [value,title]of ZYL_TIMER_DATA.operations){const option=dialogueElement('option','',title);option.value=value;select.append(option);}select.value=current.kind;
 select.onchange=()=>onChange({kind:select.value,seconds:current.seconds??0});area.append(label,select);
 if(ZYL_TIMER_DATA.needsTime(current.kind)){
  const input=dialogueElement('input');input.type='text';input.value=ZYL_TIMER_DATA.format(current.seconds);input.placeholder='05:00';input.setAttribute('aria-label','倒计时操作时间');input.onchange=()=>onChange({kind:current.kind,seconds:ZYL_TIMER_DATA.parse(input.value)});area.append(input,dialogueElement('span','','分:秒，也可填总秒数'));
 }
 parent.append(area);
}
