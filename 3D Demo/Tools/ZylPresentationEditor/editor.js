'use strict';

const standingPoses = {
    Idle:'站立待机', ChinThink:'托腮思考', TempleTouch:'扶太阳穴', Explain:'单手解释',
    OpenHands:'双手摊开', Emphasis:'食指强调', HeadClutch:'双手抱头', AngryFists:'握拳发怒',
    Slumped:'垂头沮丧', KneeBrace:'屈膝撑膝', StandWave:'站立挥手'
};
const seatedPoses = {
  ChairWave:'坐姿挥手', ChairTalk:'坐姿讲话', ChairListen:'坐姿听候',
  ChairWorry:'坐姿担忧', ChairEmphasis:'坐姿强调'
};
const motionSections = [
  {id:12,group:3,key:'encounterActionClips',dialogue:'freeDialogue',title:'自由对话',poses:seatedPoses,
   note:'接通并回应开场后开始。等待回复时继续播放当前动作，巨兽仍可打断对话。'},
  {id:13,group:7,key:'stomachActionClips',dialogue:'stomachDialogue',title:'肚中会谈',poses:standingPoses,
   note:'转舱到位后开始。等待回复时继续播放当前动作。'},
  {id:14,group:9,key:'printingActionClips',dialogue:'printingDialogue',title:'飞船内对白',poses:seatedPoses,
   note:'飞船就座后开始。等待回复时继续播放当前动作。',cue:'showPole',cueLabel:'展示短杆'},
  {id:15,group:10,key:'foolsActionClips',poses:standingPoses},
  {id:16,group:11,key:'escapeGravesActionClips',dialogue:'escapeGravesDialogue',title:'墓前对白',poses:standingPoses,
   note:'从墓前对白开始。进入拔肉芽和持握时，由演出接管动作。',cue:'pullSprout',cueLabel:'开始拔肉芽',lock:true},
  {id:17,group:12,key:'farewellActionClips',dialogue:'farewellDialogue',title:'别离视频通话',poses:standingPoses,
   note:'从通话对白开始。等待回复时继续播放当前动作，倒计时归零可打断动作。'}
];

function createActionTimeline(spec) {
  const {key,poses}=spec;
  const effectKey=key.replace('ActionClips','CameraEffects');
  const continuous=spec.id===15;
  const titles=continuous ? {ground:'离船对白',walk:'上坡',crest:'坡顶对白与绕后',transition:'黑幕转场',closing:'最后对白'} : {dialogue:spec.title};
  const trackKeys=continuous ? {ground:'foolsGroundDialogue',walk:'foolsWalkDialogue',crest:'foolsCrestDialogue',transition:null,closing:'foolsClosingDialogue'} : {dialogue:spec.dialogue};
  const editable=continuous ? ['ground','crest','closing'] : ['dialogue'];
  const effectSections=Object.keys(trackKeys).filter(section=>section!=='transition');
  const ranges = {offset:[0,3600],duration:[.1,600],speed:[.25,3],blend:[0,3],strength:[0,100]};
  const routeLength = 33.96592;
  const snapPixels = 8;
  let panel, viewport, canvas, summary, inspector, notices, selector, zoom;
  let chosen=-1, chosenEffect=-1, effectSelector, effectInspector;

  function validate(config) {
    const clips=config[key];
    if (!Array.isArray(clips) || clips.length > 64) return [`演出 ${spec.id} 动作轨需要数组，最多 64 段。`];
    const errors=clips.flatMap((c,i) => {
      if (c && editable.includes(c.section) && Object.hasOwn(poses,c.pose) &&
          ['loop','hold'].includes(c.playback) && (c.lineId == null || (typeof c.lineId==='string'&&c.lineId.length<=48)) &&
          ['offset','duration','speed','blend'].every(k=>inRange(c[k],ranges[k]))) return [];
      return [`演出 ${spec.id} 动作轨第 ${i+1} 段的动作、区段或时间参数无效。`];
    });
    const effects=config[effectKey];
    if(!Array.isArray(effects)||effects.length>64)return [...errors,`演出 ${spec.id} 镜头效果轨需要数组，最多 64 段。`];
    effects.forEach((cue,i)=>{
      if(!cue||!effectSections.includes(cue.section)||(cue.lineId!=null&&(typeof cue.lineId!=='string'||cue.lineId.length>48))||
          !['offset','duration','strength'].every(k=>inRange(cue[k],ranges[k])))
        errors.push(`演出 ${spec.id} 镜头效果轨第 ${i+1} 段的区段、时间或震动强度无效。`);
    });
    return errors;
  }

  const inRange=(value,[min,max])=>Number.isFinite(value)&&value>=min&&value<=max;
  const element = (tag,css,text) => {
    const el=document.createElement(tag);
    if(css)el.className=css;
    if(text!==undefined)el.textContent=text;
    return el;
  };
  function button(text, onClick, css) {
    const el=element('button',css,text);
    el.type='button';el.addEventListener('click',onClick);return el;
  }
  const round = n => Math.round(n*100)/100;
  const finite = (x,fallback) => Number.isFinite(x) ? x : fallback;
  const clamp = (v,a,b) => Math.min(b,Math.max(a,v));

  function layout(config) {
    const bySection={};
    let time=0;
    for(const section of Object.keys(trackKeys)){
      let lineTime=0;
      const entries=Array.isArray(config[trackKeys[section]])?config[trackKeys[section]]:[];
      const lines=entries.filter(Boolean).map((line,i)=>{
        const length=clamp(finite(line.duration,0),0,600);
        const row={line,i,start:lineTime,length};
        lineTime+=length;
        return row;
      });
      let length=lineTime;
      if(section==='walk')length=routeLength/Math.max(.2,finite(config.foolsWalkSpeed,.85));
      if(section==='transition')length=2*finite(config.foolsSceneFadeDuration,1.2)+.15;
      if(section==='crest'){
        const orbit=finite(config.foolsOrbitDuration,7);
        length=Math.max(length,finite(config.foolsFrontHold,2)+Math.max(orbit,orbit/2+finite(config.foolsPullbackDuration,5))+finite(config.foolsFinalHold,3));
      }
      const cue=spec.cue ? lines.find(row=>row.line[spec.cue]===true) : undefined;
      bySection[section]={start:time,length,lines,cue,actionLength:spec.lock&&cue ? cue.start : length};
      time+=length;
    }
    return {bySection,total:time};
  }

  function anchor(section,id) {
    if(!id)return 0;
    return section.lines.find(row=>row.line.timerKey===id)?.start;
  }

  function updateClip(index, changes, isEffect=false) {
    const next=copy(values);
    const list=next[isEffect?effectKey:key];
    if(!list?.[index])return;
    Object.assign(list[index],changes);
    if(isEffect)chosenEffect=index;
    else chosen=index;
    commit(next,false);
  }

  function addEffect(section, offset=0) {
    const next=copy(values), sections=layout(values).bySection;
    section=section??effectSections.find(name=>sections[name].length>=ranges.duration[0]);
    if(!section){notices.textContent='请先配置台词或演出时长。';return;}
    if(next[effectKey].length>=64){notices.textContent='镜头效果轨最多添加 64 段。';return;}
    chosenEffect=next[effectKey].length;
    const start=round(clamp(offset,0,Math.min(ranges.offset[1],sections[section].length-ranges.duration[0])));
    const duration=round(clamp(sections[section].length-start,ranges.duration[0],1));
    next[effectKey].push({section,lineId:'',offset:start,duration,strength:35});
    commit(next,false);
  }

  function addClip(pose, section, offset) {
    const next=copy(values);
    if(!Array.isArray(next[key])||next[key].length>=64)return;
    if(layout(values).bySection[section].actionLength<=0){
      notices.textContent='此段没有可编排的对白时间。';
      return;
    }
    chosen=next[key].length;
    next[key].push({section,lineId:'',offset:round(clamp(offset,0,3600)),duration:2,pose,speed:1,blend:.62,
      playback:['Emphasis','AngryFists','KneeBrace','StandWave'].includes(pose)?'hold':'loop'});
    commit(next,false);
  }

  function init() {
    const group=document.getElementById('group'+spec.group);
    if(!group)return;
    panel=element('section','action-timeline');
    panel.id=continuous?'fools-action-timeline':`action-timeline-${spec.id}`;
    const heading=element('div','timeline-heading');
    const title=element('div');
    title.append(element('span','eyebrow',`SETPIECE ${spec.id} / MOTION`),element('h3','','米霍尔动作时间轴'));
    const zoomLabel=element('label','timeline-zoom','时间轴缩放 ');
    zoom=element('input');
    Object.assign(zoom,{type:'range',min:6,max:48,value:16});
    zoom.addEventListener('input',refresh);
    zoomLabel.append(zoom);
    const viewControls=element('div','timeline-view-controls');
    viewControls.append(zoomLabel,button('适配整段',fit));
    heading.append(title,viewControls);
    summary=element('p','timeline-note');
    const body=element('div','timeline-body');
    const labels=element('div','timeline-labels');
    for(const name of ['时间','台词','演出节点','动作轨','镜头效果'])labels.append(element('div','',name));
    viewport=element('div','timeline-viewport');viewport.tabIndex=0;
    viewport.setAttribute('aria-label','可横向滚动的演出时间轴');
    canvas=element('div','timeline-canvas');viewport.append(canvas);body.append(labels,viewport);
    const editBar=element('div','timeline-edit-bar');
    selector=element('select');selector.setAttribute('aria-label','选择动作段');
    selector.addEventListener('change',()=>{chosen=Number(selector.value);refresh();});
    editBar.append(element('span','','动作细调'),selector);
    inspector=element('div','timeline-inspector');
    const effectBar=element('div','timeline-edit-bar');
    effectSelector=element('select');
    effectSelector.setAttribute('aria-label','选择镜头震动段');
    effectSelector.addEventListener('change',()=>{chosenEffect=Number(effectSelector.value);refresh();});
    effectBar.append(element('span','','镜头效果细调'),effectSelector,button('添加镜头震动',()=>addEffect()));
    effectInspector=element('div','timeline-inspector');
    notices=element('div','timeline-notices');notices.setAttribute('role','status');
    const pool=element('div','timeline-pool');
    pool.append(element('strong','',poses===seatedPoses?'坐姿动作池':'站姿动作池'),element('p','timeline-note','拖到动作轨，或点一下添加。拖动作块移动，拖两端调整时长。靠近刻度、台词边界或节点会自动对齐，按住 Alt 可微调。'));
    const choices=element('div','timeline-choices');
    for(const [id,label] of Object.entries(poses)){
      const item=button(label,()=>{
        const selection=values[key]?.[chosen];
        addClip(id,selection?.section??editable[0],0);
      });
      item.draggable=true;
      item.addEventListener('dragstart',event=>{
        event.dataTransfer.setData('text/x-zyl-pose',id);event.dataTransfer.effectAllowed='copy';
      });
      choices.append(item);
    }
    pool.append(choices);
    panel.append(heading,summary,body,editBar,inspector,effectBar,effectInspector,notices,pool);
    group.querySelector('.grid').before(panel);
    const link=element('a','timeline-rail-link',`↳ ${spec.id} · 米霍尔动作时间轴`);
    link.href='#'+panel.id;
    document.querySelector(`#nav a[href="#group${spec.group}"]`).after(link);
    fit();
  }

  function fit() {
    const available=viewport.clientWidth-48;
    zoom.value=clamp(Math.floor(available/Math.max(layout(values).total,1)),Number(zoom.min),Number(zoom.max));
    viewport.scrollLeft=0;
    refresh();
  }

  function refresh() {
    if(!panel)return;
    const model=layout(values), {bySection:sections,total}=model;
    const clips=Array.isArray(values[key])?values[key]:[];
    if(chosen>=clips.length)chosen=clips.length-1;
    const effects=Array.isArray(values[effectKey])?values[effectKey]:[];
    if(chosenEffect>=effects.length)chosenEffect=effects.length-1;
    const scale=Math.min(Number(zoom.value),50000/Math.max(total,1));
    const x=t=>t*scale;
    const width=Math.max(x(total)+48,viewport.clientWidth);
    canvas.style.width=width+'px';
    canvas.replaceChildren();
    summary.textContent=continuous ? `从离船开始 · 预计 ${round(total)} 秒 · 上坡约 ${round(sections.walk.length)} 秒。到坡顶切断上坡对白，黑幕转场后开始最后对白。` : `预计 ${round(total)} 秒（按台词时长）。${spec.note}`;
    const snapPoints=[];
    const ticks=[];
    const ruler=element('div','timeline-lane ruler');
    const tick=Math.max(scale<10?10:scale<22?5:2,Math.ceil(total/75));
    for(let t=0;t<=total;t+=tick){
      const mark=element('span','timeline-tick',t+'秒');
      mark.style.left=x(t)+'px';ruler.append(mark);
      ticks.push({time:t,label:`${t} 秒刻度`});
    }
    canvas.append(ruler);
    const dialogue=element('div','timeline-lane dialogue-lane');
    const nodes=element('div','timeline-lane node-lane');
    const actions=element('div','timeline-lane action-lane');
    const cameraLane=element('div','timeline-lane camera-lane');
    cameraLane.addEventListener('dblclick',event=>{
      if(event.target!==cameraLane)return;
      const time=(event.clientX-cameraLane.getBoundingClientRect().left)/scale;
      const section=effectSections.find(name=>time>=sections[name].start&&time<sections[name].start+sections[name].length);
      if(section)addEffect(section,time-sections[section].start);
    });
    const nodeNames={ground:'离船',walk:'开始上坡',crest:'到达坡顶',transition:'黑幕转场',closing:'最后对白开始',dialogue:'开始对白'};
    for(const [name,section] of Object.entries(sections)){
      snapPoints.push({time:section.start,label:nodeNames[name]});
    }
    snapPoints.push({time:total,label:'动作时间轴末尾'});
    for(const [name,section] of Object.entries(sections)){
      const label=element('span','timeline-node',nodeNames[name]);
      label.style.left=x(section.start)+'px';nodes.append(label);
      if(section.cue){
        const time=section.start+section.cue.start;
        if(section.cue.start===0)label.textContent+=' · '+spec.cueLabel;
        else {
          const marker=element('span','timeline-node',spec.cueLabel);
          marker.style.left=x(time)+'px';nodes.append(marker);
        }
        snapPoints.push({time,label:spec.cueLabel});
      }
      for(const row of section.lines){
        const length=Math.min(row.length,section.length-row.start);
        if(length<=0)break;
        const item=button(`${row.i+1}. ${row.line.text||'台词'}`,()=>document.getElementById(trackKeys[name]+'-text-'+row.i)?.focus(),'timeline-dialogue');
        item.title=`${titles[name]} · ${row.line.text||''}${length<row.length?'（预计到坡顶时打断）':''}`;
        item.style.left=x(section.start+row.start)+'px';
        item.style.width=Math.max(3,x(length))+'px';dialogue.append(item);
        const lineLabel=`${titles[name]} · 第 ${row.i+1} 句`;
        snapPoints.push(
          {time:section.start+row.start,label:lineLabel+'开始'},
          {time:section.start+row.start+length,label:lineLabel+'结束'}
        );
      }
    }
    snapPoints.push(...ticks);
    const guide=element('div','timeline-snap-guide');
    const guideLabel=element('span','timeline-snap-label');
    guide.hidden=true;
    guide.setAttribute('aria-hidden','true');
    guide.append(guideLabel);
    const lockLane=(section,start,label,lane=actions)=>{
      const lock=element('div','timeline-locked',label);
      lock.style.left=x(section.start+start)+'px';lock.style.width=x(section.length-start)+'px';
      lane.append(lock);
    };
    if(continuous){
      lockLane(sections.walk,0,'🔒 坡度驱动步态');
      lockLane(sections.transition,0,'🔒 黑幕转场');
      lockLane(sections.transition,0,'黑幕转场',cameraLane);
    }else if(sections.dialogue.actionLength<sections.dialogue.length){
      lockLane(sections.dialogue,sections.dialogue.actionLength,'🔒 拔取与持握');
    }
    actions.addEventListener('dragover',event=>{
      if(event.dataTransfer.types.includes('text/x-zyl-pose'))event.preventDefault();
    });
    actions.addEventListener('drop',event=>{
      const pose=event.dataTransfer.getData('text/x-zyl-pose');
      if(!Object.hasOwn(poses,pose))return;
      event.preventDefault();
      const time=(event.clientX-actions.getBoundingClientRect().left)/scale;
      const section=editable.find(name=>{
        const lane=sections[name];
        return time>=lane.start && time<lane.start+lane.actionLength;
      });
      if(section)addClip(pose,section,time-sections[section].start);
      else notices.textContent='请将动作拖到可编辑的对白区间。';
    });
    const warnings=[];
    function drawLane(items,lane,allowed,isEffect) {
      items.forEach((clip,index)=>{
        if(!clip||!allowed.includes(clip.section))return;
        const section=sections[clip.section], at=anchor(section,clip.lineId);
        const label=isEffect?'震动':'动作';
        if(at===undefined){warnings.push(`${label} ${index+1} 的台词已删除，可重新选择锚点。`);return;}
        const start=at+finite(clip.offset,0), end=start+finite(clip.duration,0);
        const limit=isEffect?section.length:section.actionLength;
        const length=Math.min(end,limit)-start;
        if(length<=0){warnings.push(`${label} ${index+1} 位于区段结束之后。`);return;}
        if(end>limit+.001)warnings.push(`${label} ${index+1} 会在区段结束时截断。`);
        const name=isEffect?`镜头震动 ${clip.strength}%`:poses[clip.pose]||'动作';
        const selected=(isEffect?chosenEffect:chosen)===index;
        const bar=button(name,()=>{
          if(isEffect)chosenEffect=index;
          else chosen=index;
          refresh();
        },'timeline-action'+(isEffect?' timeline-effect':'')+(selected?' selected':''));
        bar.title=`${name} · ${round(start)}秒起 · ${round(length)}秒`;
        bar.style.left=x(section.start+start)+'px';bar.style.width=Math.max(8,x(length))+'px';
        const leftHandle=element('span','timeline-trim trim-start');
        const rightHandle=element('span','timeline-trim trim-end');
        leftHandle.dataset.edge='start';leftHandle.title='调整开始位置';
        rightHandle.dataset.edge='end';rightHandle.title='调整结束位置';
        bar.append(leftHandle,rightHandle);
        bar.addEventListener('pointerdown',event=>drag(event,bar,clip,{
          scale,snapPoints,guide,guideLabel,section:{start:section.start,length:limit},start,end,anchorTime:at
        },changes=>updateClip(index,changes,isEffect)));
        lane.append(bar);
      });
    }
    drawLane(clips,actions,editable,false);
    drawLane(effects,cameraLane,effectSections,true);
    canvas.append(dialogue,nodes,actions,cameraLane,guide);
    notices.textContent=warnings.join(' ');
    selector.replaceChildren(new Option('请选择动作',-1),...clips.map((clip,i)=>new Option(`${i+1}. ${poses[clip?.pose]??'无效动作'} · ${titles[clip?.section]??'区段'}`,i)));
    selector.value=String(chosen);
    drawInspector(clips[chosen],sections,false);
    effectSelector.replaceChildren(new Option('请选择震动',-1),...effects.map((cue,i)=>
      new Option(`${i+1}. 镜头震动 · ${titles[cue?.section]??'区段'}`,i)));
    effectSelector.value=String(chosenEffect);
    drawInspector(effects[chosenEffect],sections,true);
  }

  function drag(event,bar,clip,track,save) {
    if(event.button!==0||!event.isPrimary)return;
    const {scale,snapPoints,guide,guideLabel,section,start,end,anchorTime}=track;
    const edge=event.target.dataset.edge;
    const pointerStart=event.clientX;
    const canvasStart=canvas.getBoundingClientRect().left;
    const visibleEnd=Math.min(end,section.length);
    const [minDuration,maxDuration]=ranges.duration;
    const maxStart=Math.min(anchorTime+ranges.offset[1],section.length-minDuration);
    const min= edge==='end' ? Math.min(start+minDuration,visibleEnd) :
      edge==='start' ? Math.max(anchorTime,end-maxDuration) : Math.min(anchorTime,start);
    const max= edge==='end' ? Math.min(start+maxDuration,section.length) :
      edge==='start' ? Math.max(start,Math.min(maxStart,end-minDuration)) : Math.max(start,maxStart);
    const movingTime=edge==='end'?visibleEnd:start;
    const offsets=edge ? [0] : [0,clip.duration];
    let pointerX=pointerStart, active=false, changes;

    function preview(altKey) {
      const pixels=pointerX-pointerStart+canvasStart-canvas.getBoundingClientRect().left;
      if(!active&&Math.abs(pixels)<3)return;
      active=true;
      const desired=clamp(movingTime+pixels/scale,min,max);
      let time=desired, snapped=null, best=snapPixels+.001;
      if(!altKey){
        for(const point of snapPoints){
          if(point.time<section.start||point.time>section.start+section.length)continue;
          for(const offset of offsets){
            const candidate=point.time-section.start-offset;
            const distance=Math.abs(candidate-desired)*scale;
            if(candidate<min-1e-9||candidate>max+1e-9||distance>=best-1e-9)continue;
            time=clamp(candidate,min,max);snapped=point;best=distance;
          }
        }
      }
      const precision=snapped ? value=>Math.round(value*1e9)/1e9 : round;
      time=clamp(precision(time),min,max);
      const nextStart=edge==='end'?start:time;
      const nextEnd=edge==='end'?time:edge==='start'?end:nextStart+clip.duration;
      changes={};
      if(edge!=='end')changes.offset=Math.max(0,nextStart-anchorTime);
      if(edge)changes.duration=clamp(nextEnd-nextStart,minDuration,maxDuration);
      bar.style.left=(section.start+nextStart)*scale+'px';
      bar.style.width=Math.max(8,(Math.min(nextEnd,section.length)-nextStart)*scale)+'px';
      guide.hidden=!snapped;
      if(snapped){
        const guideX=snapped.time*scale;
        guide.style.left=guideX+'px';
        guideLabel.textContent=snapped.label;
        const labelLeft=clamp(guideX+6,viewport.scrollLeft+4,
          Math.max(viewport.scrollLeft+4,viewport.scrollLeft+viewport.clientWidth-guideLabel.offsetWidth-4));
        guideLabel.style.left=(labelLeft-guideX)+'px';
      }
    }

    function altChanged(e) {
      if(e.key==='Alt'&&active)preview(e.type==='keydown');
    }
    function finish() {
      window.removeEventListener('keydown',altChanged);
      window.removeEventListener('keyup',altChanged);
      bar.onpointermove=bar.onpointerup=bar.onpointercancel=bar.onlostpointercapture=null;
      guide.hidden=true;
    }

    bar.setPointerCapture(event.pointerId);
    window.addEventListener('keydown',altChanged);
    window.addEventListener('keyup',altChanged);
    bar.onpointermove=e=>{
      pointerX=e.clientX;preview(e.altKey);
    };
    bar.onpointercancel=bar.onlostpointercapture=()=>{finish();refresh();};
    bar.onpointerup=e=>{
      pointerX=e.clientX;preview(e.altKey);finish();
      if(!active)return;
      if(Object.keys(changes).some(key=>Math.abs(changes[key]-clip[key])>1e-9))save(changes);
      else refresh();
    };
  }

  function drawInspector(clip,sections,isEffect) {
    const box=isEffect?effectInspector:inspector;
    const index=isEffect?chosenEffect:chosen;
    const allowed=isEffect?effectSections:editable;
    box.replaceChildren();
    if(!clip){
      box.append(element('p','timeline-note',isEffect?'可双击效果轨的空白处添加震动。拖动或拉伸时沿用时间轴磁吸。':'动作轨空白处沿用原表演。重叠时，较晚开始的动作优先。'));
      return;
    }
    const section=sections[clip.section]??sections[allowed[0]];
    const update=changes=>updateClip(index,changes,isEffect);
    const select=(label,field,choices)=>{
      const wrapper=element('label','',label);
      const input=element('select');
      for(const [value,name] of choices)input.add(new Option(name,value));
      if(![...input.options].some(option=>option.value===(clip[field]??'')))input.add(new Option('原绑定已失效',clip[field]));
      input.value=clip[field]??'';
      input.addEventListener('change',()=>update(field==='section'?{section:input.value,lineId:'',offset:0}:{[field]:input.value}));
      wrapper.append(input);box.append(wrapper);
    };
    if(continuous)select('区段','section',allowed.map(name=>[name,titles[name]]));
    const starts={ground:'离船开始',walk:'开始上坡',crest:'到达坡顶',closing:'最后对白开始',dialogue:'开始对白'};
    select('开始锚点','lineId',[['',starts[clip.section]??'区段开始'],
      ...section.lines.filter(row=>row.line.timerKey).map(row=>[row.line.timerKey,`第 ${row.i+1} 句 · ${(row.line.text||'').slice(0,18)}`])]);
    if(!isEffect){
      select('动作','pose',Object.entries(poses));
      select('播放方式','playback',[['loop','循环播放'],['hold','播完保持末帧']]);
    }
    const fields=[['offset','锚点后（秒）'],['duration',isEffect?'震动时长（秒）':'占用时长（秒）']];
    if(isEffect)fields.push(['strength','震动强度（%）']);
    else fields.push(['speed','播放速度（倍）'],['blend','混合时长（秒）']);
    for(const [field,label] of fields){
      const wrapper=element('label','',label);
      const input=element('input');
      Object.assign(input,{type:'number',min:ranges[field][0],max:ranges[field][1],step:'any',value:clip[field]});
      input.addEventListener('change',()=>update({[field]:input.value===''?null:Number(input.value)}));
      wrapper.append(input);box.append(wrapper);
    }
    box.append(button(isEffect?'删除震动':'删除动作',()=>{
      const next=copy(values);next[isEffect?effectKey:key].splice(index,1);
      if(isEffect)chosenEffect=-1;
      else chosen=-1;
      commit(next,false);
    }));
  }

  return {init,refresh,validate};
}

const motionTimelines=motionSections.map(createActionTimeline);

const {groups,fields,defaults,rules}=window.ZYL_PARAMETERS;
const $=id=>document.getElementById(id), legacyDraftKey='astra-zyl-presentation-v1';
const pageKey=location.pathname, draftKey=legacyDraftKey+':'+pageKey;
let values=ZYL_DIALOGUE.normalize(defaults,defaults),history=[],future=[],handle=null,baseline=null,revision=0,savedRevision=0,filename='未关联文件',busy=false;
let rememberedFile=null,fileId=null,fileConflict=false,cachedFile=false;
const copy=v=>JSON.parse(JSON.stringify(v)), locationUI=EditorFileLocation, localFiles=window.EditorLocalFiles;
let currentFile=null,lastSave=null;
function labelFile(file){
 if(currentFile?.id===file.id)currentFile={...currentFile,path:file.path};
 if(lastSave?.id===file.id)lastSave={...lastSave,path:file.path};
 persist();renderFileLocation();
}
function renderFileLocation(){
 locationUI.render(currentFile,lastSave,labelFile,busy);
}
function status(message){
 const state=revision===savedRevision?(cachedFile&&!handle?'浏览器副本':'已保存 / 默认值'):'有未保存修改';
 const association=fileConflict?' · 与磁盘版本冲突，直接保存已阻止':
     handle?' · 已关联文件':rememberedFile?' · 请重新连接上次文件':' · 请打开配置后保存，或下载 JSON';
 $('status').textContent=message||`${filename} · ${state}${association}`;
 $('reconnect').hidden=!rememberedFile||!!handle;
 renderFileLocation();
}
function validate(v){const errors=motionTimelines.flatMap(timeline=>timeline.validate(v)).concat(ZYL_DIALOGUE.validate(v.farewellDialogue)).concat(ZYL_DIALOGUE.validate(v.freeDialogue)).concat(ZYL_DIALOGUE.validate(v.stomachDialogue).map(e=>e.replaceAll("自由对话","肚中会谈")),ZYL_DIALOGUE.validate(v.printingDialogue).map(e=>e.replaceAll("自由对话","打印构件对白")));for(const key of ['foolsGroundDialogue','foolsWalkDialogue','foolsCrestDialogue','foolsClosingDialogue','escapeGravesDialogue','escapeEncouragement']){errors.push(...ZYL_DIALOGUE.validate(v[key]).map(e=>e.replaceAll('自由对话',key)));if(Array.isArray(v[key])&&v[key].some(line=>line?.replies?.length))errors.push('连续演出对白不支持玩家回复。');}if(Array.isArray(v.escapeGravesDialogue)){const lines=v.escapeGravesDialogue;if(lines.some(line=>line&&Object.hasOwn(line,'pullSprout')&&typeof line.pullSprout!=='boolean'))errors.push('墓前对白的拔肉芽勾选必须是布尔值。');if(lines.filter(line=>line?.pullSprout===true).length>1)errors.push('只能勾选一句台词开始拔肉芽。');}if(Array.isArray(v.printingDialogue)){if(v.printingDialogue.some(line=>line&&Object.hasOwn(line,'showPole')&&typeof line.showPole!=='boolean'))errors.push('短杆展示勾选必须是布尔值。');if(v.printingDialogue.filter(line=>line?.showPole===true).length>1)errors.push('只能勾选一句台词开始展示短杆。');}if(v.printingPoleCueConfigured!==true)errors.push('短杆展示配置格式无效。');if(v.schemaVersion!==1)errors.push('只支持 schemaVersion 1。');if(!['v1','v2'].includes(v.farewellIllustrationVersion))errors.push('请选择有效的爆炸插画版本。');for(const f of fields){let n=v[f.key];if(f.type==='frameSelection'){for(const selection of [n,v.farewellIllustrationFramesV2])if(!Array.isArray(selection)||selection.length!==f.options.length||selection.some(flag=>typeof flag!=='boolean'))errors.push('每版爆炸插画需要六张画面的勾选状态。');continue;}if(f.type==='boolean'){if(typeof n!=='boolean')errors.push(`${f.label}：请选择开启或关闭。`);continue;}if(f.type==='color'){if(typeof n!=='string'||!/^#[0-9a-fA-F]{6}$/.test(n))errors.push(`${f.label}：需要#RRGGBB颜色。`);continue;}if(f.type==='text'){if(typeof n!=='string'||!n.trim()||n.length>f.maxLength||/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F-\x9F]/.test(n))errors.push(`${f.label}：请填写1–${f.maxLength}个字符的台词，支持换行。`);continue;}if(typeof n!=='number'||!Number.isFinite(n)||n<f.min||n>f.max)errors.push(`${f.label}：请输入 ${f.min}–${f.max} 之间的数值。`);}if(!Number.isInteger(v.starCount))errors.push('通话星点数量必须是整数。');for(const [a,b]of rules)if(v[a]>=v[b])errors.push(`${fields.find(f=>f.key===a).label}必须小于${fields.find(f=>f.key===b).label}。`);return errors;}
function persist(){
 try{
  const dirty=revision!==savedRevision;
  localStorage.setItem(draftKey,JSON.stringify({values,dirty,fileId,baseline,filename,cachedFile,currentFile,lastSave}));
 }catch{status('浏览器草稿不可用，请及时保存或下载。');}
}
function commit(next,renderDialogue=true){history.push(copy(values));if(history.length>100)history.shift();future=[];values=next;revision++;persist();refresh();if(renderDialogue)renderDialogueBlocks();}
function illustrationFramesKey(config){
 return config.farewellIllustrationVersion==='v2'?'farewellIllustrationFramesV2':'farewellIllustrationFrames';
}
function buildFrameSelection(card,f){
 const versionRow=document.createElement('label');
 versionRow.className='illustration-version';
 versionRow.textContent='插画版本';
 const version=document.createElement('select');
 version.id='farewellIllustrationVersion';
 version.setAttribute('aria-label','插画版本');
 version.append(new Option('原版 V1','v1'),new Option('新版 V2 · 舷窗爆炸','v2'));
 version.onchange=()=>commit({...values,farewellIllustrationVersion:version.value},false);
 versionRow.append(version);
 card.querySelector('.desc').after(versionRow);
 const choices=document.createElement('fieldset');
 choices.id=f.key;
 choices.setAttribute('aria-label',f.label);
 for(const [index,title] of f.options.entries()){
  const label=document.createElement('label');
  const input=document.createElement('input');
  const caption=document.createElement('span');
  caption.textContent=`${index+1} · ${title}`;
  input.type='checkbox';
  input.onchange=()=>{
   const next=copy(values);
   const key=illustrationFramesKey(next);
   const selected=Array.isArray(next[key])?next[key]:defaults[key].slice();
   selected[index]=input.checked;
   next[key]=selected;
   commit(next,false);
  };
  label.append(input,caption);
  choices.append(label);
 }
 card.classList.add('frame-selection-card');
 card.querySelector('.value-row input').replaceWith(choices);
 return choices;
}
const farewellV2FrameNames = ['蓝白高闪', '白底黑影闪', '黑底白裂闪', '冷蓝回落', '余光收缩', '残光熄灭'];
function refreshFrameSelection(input,f){
 const version=$('farewellIllustrationVersion');
 version.value=values.farewellIllustrationVersion;
 const isV2=version.value==='v2';
 const selected=values[illustrationFramesKey(values)];
 const boxes=Array.from(input.querySelectorAll('input'));
 let count=0;
 for(const [index,box] of boxes.entries()){
  box.checked=selected?.[index]===true;
  box.nextElementSibling.textContent=isV2?`${index+1} · ${farewellV2FrameNames[index]}`:`${index+1} · ${f.options[index]}`;
  if(box.checked)count++;
 }
 input.nextElementSibling.textContent=`已选 ${count} / ${boxes.length} 张`;
 $(f.key+'hint').textContent=isV2?'新版包含舷窗和两张短促黑白闪，默认使用全部六张。':'原版默认使用全部六张。';
}
function refresh(){for(const f of fields){const input=$(f.key);if(f.type==='frameSelection')refreshFrameSelection(input,f);else if(f.type==='boolean'){input.checked=values[f.key]===true;input.nextElementSibling.textContent=input.checked?'已启用':'已关闭';}else if(document.activeElement!==input)input.value=f.type==='time'?ZYL_TIMER_DATA.format(values[f.key]):['text','color'].includes(f.type)?(values[f.key]??''):(Number.isFinite(values[f.key])?values[f.key]:'');input.closest('.card').classList.toggle('changed',f.type==='frameSelection'?['farewellIllustrationVersion',f.key,'farewellIllustrationFramesV2'].some(key=>JSON.stringify(values[key])!==JSON.stringify(defaults[key])):values[f.key]!==f.value);}$('undo').disabled=!history.length;$('redo').disabled=!future.length;$('errors').textContent=validate(values).join('\n');$('summary').replaceChildren(...[['巨兽初始口部距离',values.initialClearance,'单位'],['全黑加载触发距离',values.loadDistance,'单位'],['胃内淡入时长',values.fadeInDuration,'秒']].map(([s,n,u])=>{let d=document.createElement('div');d.className='metric';d.textContent=s;let b=document.createElement('b');b.textContent=Number.isFinite(n)?n:'—';let small=document.createElement('small');small.textContent=u;b.append(small);d.append(b);return d;}));status();motionTimelines.forEach(timeline=>timeline.refresh());}
groups.forEach((g,i)=>{let link=document.createElement('a');link.href='#group'+i;link.textContent=g;$('nav').append(link);let section=document.createElement('section');section.className='group';section.id='group'+i;let heading=document.createElement('div');heading.className='group-title';let h=document.createElement('h3');h.textContent=g;let reset=document.createElement('button');reset.textContent='恢复本组默认值';reset.onclick=()=>{const next=copy(values);fields.filter(f=>f.group===g).forEach(f=>next[f.key]=f.value);if(i===3)next.freeDialogue=copy(defaults.freeDialogue);if(i===7)next.stomachDialogue=copy(defaults.stomachDialogue);if(i===9)next.printingDialogue=copy(defaults.printingDialogue);const motion=motionSections.find(spec=>spec.group===i);if(motion){next[motion.key]=copy(defaults[motion.key]);const effectKey=motion.key.replace('ActionClips','CameraEffects');next[effectKey]=copy(defaults[effectKey]);}if(i===10)for(const key of ['foolsGroundDialogue','foolsWalkDialogue','foolsCrestDialogue','foolsClosingDialogue'])next[key]=copy(defaults[key]);if(i===12)for(const key of ['farewellDialogue','farewellIllustrationVersion','farewellIllustrationFramesV2'])next[key]=copy(defaults[key]);if(i===11)for(const key of ['escapeGravesDialogue','escapeEncouragement'])next[key]=copy(defaults[key]);commit(next);};heading.append(h,reset);section.append(heading);let grid=document.createElement('div');grid.className='grid';fields.filter(f=>f.group===g).forEach(f=>{let card=document.createElement('article');card.className='card';card.dataset.search=[f.label,f.desc,f.key].join(' ').toLowerCase();card.innerHTML='<div class="field-top"><label></label><span class="tag"></span></div><div class="id"></div><div class="desc"></div><div class="value-row"><input type="number"><span></span><button type="button">还原</button></div><div class="range"></div>';let label=card.querySelector('label');label.textContent=f.label;label.htmlFor=f.key;card.querySelector('.tag').textContent=f.unit;card.querySelector('.id').textContent=f.key;card.querySelector('.desc').textContent=f.desc;if(f.type==='text'){card.classList.add('text-card');card.querySelector('.value-row').innerHTML='<textarea rows=4></textarea><span></span><button type=button>还原</button>';}let input=f.type==='frameSelection'?buildFrameSelection(card,f):card.querySelector(f.type==='text'?'textarea':'input');if(f.type==='frameSelection')label.removeAttribute('for');else if(f.type==='boolean')Object.assign(input,{id:f.key,type:'checkbox'});else if(f.type==='text')Object.assign(input,{id:f.key,maxLength:f.maxLength,required:true});else if(f.type==='color')Object.assign(input,{id:f.key,type:'color',value:f.value,required:true});else if(f.type==='time')Object.assign(input,{id:f.key,type:'text',placeholder:'00:05:00',required:true});else Object.assign(input,{id:f.key,min:f.min,max:f.max,step:f.key==='starCount'?'1':'any',required:true});input.setAttribute('aria-describedby',f.key+'hint');if(f.type!=='frameSelection')input.addEventListener('change',()=>{let next=copy(values);next[f.key]=f.type==='boolean'?input.checked:f.type==='time'?ZYL_TIMER_DATA.parse(input.value):['text','color'].includes(f.type)?input.value:(input.value.trim()===''?null:Number(input.value));commit(next);});card.querySelector('.value-row span').textContent=f.unit;card.querySelector('button').onclick=()=>{const next=copy(values);if(f.type==='frameSelection'){const key=illustrationFramesKey(next);next[key]=copy(defaults[key]);}else next[f.key]=f.value;commit(next);};let hint=card.querySelector('.range');hint.id=f.key+'hint';hint.textContent=f.type==='frameSelection'?'默认使用全部六张':f.type==='boolean'?`默认 ${f.value?'开启':'关闭'}`:f.type==='time'?`允许 ${ZYL_TIMER_DATA.format(f.min)}–${ZYL_TIMER_DATA.format(f.max)} · 默认 ${ZYL_TIMER_DATA.format(f.value)}`:f.type==='color'?`默认 ${f.value}`:f.type==='text'?`最多 ${f.maxLength} 字符 · 可换行 · 点击“还原”恢复原台词`:`建议及允许范围 ${f.min}–${f.max} · 默认 ${f.value}`;grid.append(card);});section.append(grid);$('sections').append(section);});
$('count').textContent=fields.length+' 个固定参数 + 自由对话段落';
initDialogueEditor();renderDialogueBlocks();
motionTimelines.forEach(timeline=>timeline.init());
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
  await recentFileRequest('readwrite',store=>rememberedFile?store.put({...rememberedFile,handle:localFiles?.store(rememberedFile.handle) ?? rememberedFile.handle},pageKey):store.delete(pageKey));
  return true;
 }catch{return false;}
}

async function importText(text,name,newHandle){
 const next=parseConfig(text);
 const previous=handle||rememberedFile?.handle;
 const same=!!newHandle&&!!previous&&await newHandle.isSameEntry(previous);
 currentFile=same&&currentFile?currentFile:locationUI.newFile(name,newHandle);
 handle=newHandle;
 fileId=handle?(same&&fileId?fileId:currentFile.id):null;
 baseline=text;
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
   currentFile=draft.currentFile??(cachedFile?locationUI.newFile(filename):null);
   lastSave=draft.lastSave??null;
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
 currentFile??=locationUI.newFile(file.name,handle);
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
  rememberedFile={...record,handle:localFiles?.restore(record.handle) ?? record.handle};
  if(await rememberedFile.handle.queryPermission({mode:'read'})==='granted')await reopenRemembered();
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
async function guarded(action){if(busy)return;busy=true;renderFileLocation();for(const id of ['open','reconnect','save','saveas','export'])$(id).disabled=true;try{await action();}catch(e){if(e.name!=='AbortError')status('操作失败：'+e.message);}finally{busy=false;renderFileLocation();for(const id of ['open','reconnect','save','saveas','export'])$(id).disabled=false;}}
$('open').onclick=()=>guarded(async()=>{if(revision!==savedRevision&&!confirm('当前有未保存修改，仍要打开其他配置？草稿将被替换。'))return;const pick=localFiles?.open || window.showOpenFilePicker?.bind(window);if(pick){let [selected]=await pick({types:[{description:'ZYL 演出参数',accept:{'application/json':['.json']}}],multiple:false});let file=await selected.getFile();await importText(await file.text(),file.name,selected);}else $('file').click();});
$('file').onchange=()=>guarded(async()=>{let f=$('file').files[0];if(f)await importText(await f.text(),f.name,null);$('file').value='';});
function serialized(){if(document.activeElement && typeof document.activeElement.blur === 'function')document.activeElement.blur();let errors=validate(values);if(errors.length)throw Error('请先修正参数：\n'+errors.join('\n'));return JSON.stringify(values,null,2)+'\n';}
function download(){let text=serialized(),url=URL.createObjectURL(new Blob([text],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='presentation.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),10000);status('已生成下载；请确认浏览器下载完成并将 presentation.json 放回源码目录。');}
async function save(as){
 const text=serialized(),atRevision=revision;
 const pick=localFiles?.save || window.showSaveFilePicker?.bind(window);
 if(!pick){download();return;}
 if(!as&&!handle&&rememberedFile)throw Error('请先重新连接上次文件，或使用另存为。');
 let selected=handle;
 if(as||!selected)selected=await pick({suggestedName:'presentation.json',types:[{description:'ZYL 演出参数',accept:{'application/json':['.json']}}]});
 const previous=handle||rememberedFile?.handle;
 const same=!!previous&&(selected===previous||await selected.isSameEntry(previous));
 if(same&&(fileConflict||baseline===null||(!handle&&fileId!==rememberedFile.id)))
  throw Error('文件与草稿的原始版本不一致。请重新打开配置，或另存为其他文件。');
 if(!as&&selected===handle&&await selected.requestPermission({mode:'readwrite'})!=='granted')
  throw Error('未取得写入权限。');
 const possibleOriginal=!previous&&cachedFile&&selected.name.toLowerCase()===filename.toLowerCase();
 if((same||possibleOriginal)&&await (await selected.getFile()).text()!==baseline){
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
 if(!same||!currentFile)currentFile=locationUI.newFile(selected.name,selected);
 if(!same)fileId=currentFile.id;
 lastSave={...currentFile,at:Date.now()};
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
