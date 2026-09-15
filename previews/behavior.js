// Mirrors EffectBehavior.cs; checked against fixtures exported by the compiled C# sampler.
function behaviorPosition(f,p,t,key,r,h,twist,night){
  const q=hash(key+43)*6.283185, angle=q+t*(.16+night*.07)*twist;
  const reach={magic:1,treasure:.65,plant:.38,spore:1,berry:.15,mineral:.13,obsidian:.13,hive:.22,chest:.26,drift:.25};
  const height=h*reach[f], sin=Math.sin, cos=Math.cos;
  switch(f){
    case 'plant': return {x:cos(q)*r*.45+sin(t*.6+q)*r*.22*twist*p,y:.025+p*height,z:sin(q)*r*.45+cos(t*.4+q)*r*.18*twist*p};
    case 'spore': {const cloud=r*(.18+.65*(1-p))*(1+night*.1);return {x:cos(angle)*cloud,y:.05+p*height,z:sin(angle)*cloud}}
    case 'berry': return {x:cos(q)*r*.8+sin(angle*2)*r*.06*twist,y:.02+hash(key+8)*height*.6+p*height*.3,z:sin(q)*r*.8};
    case 'mineral': return {x:sin(t*.25+q)*.035*twist*p,y:.012+p*height,z:cos(q)*.035*p};
    case 'obsidian': return {x:sin(angle*1.5)*.08*twist*p,y:.012+p*height,z:cos(angle)*.06*twist*p};
    case 'hive': return {x:cos(angle+p*6.28*twist)*r*(.65+.2*sin(q)),y:.04+height*(.3+.24*sin(angle*2+q)),z:sin(angle+p*6.28*twist)*r*.65};
    case 'chest': {const edge=hash(key+2)*2-1;return {x:key%2===0?edge*r:(key%4===1?r:-r),y:.015+p*height,z:key%2!==0?edge*r*.6:(key%4===0?r:-r)*.6}}
    case 'treasure': return {x:cos(angle+p*3*twist)*r*(.3+.4*sin(p*3.14)),y:.04+p*height,z:sin(angle+p*3*twist)*r*.6};
    case 'magic': if(key%3===0)return {x:cos(angle*2)*r*.8,y:height*(.18+.05*sin(angle*2+q)),z:sin(angle*2)*r*.8};return position(p,t,key,r,h,twist);
    default:return {x:cos(angle)*r*.55,y:.03+p*height,z:sin(angle)*r*.55};
  }
}
function behaviorPath(f,column,anchor,p,t,key,r,h,twist,night){
  const v=column?position(p,t,key,r,h,twist):behaviorPosition(f,p,t,key,r,h,twist,night), k=(column||f==='spore')?(1-p)**2:1;
  return {x:v.x+anchor.x*k,y:v.y+anchor.y*k,z:v.z+anchor.z*k};
}
function behaviorAlpha(f,p,t,key){
  let a=clamp(p/.12)*clamp((1-p)/.2);
  if(f==='berry')a*=Math.max(0,Math.sin(t*1.1+Math.trunc(key/4)))**4;
  if(f==='spore')a*=.5+.5*Math.sin(p*3.14159);
  if(f==='magic'||f==='treasure')a*=.72+.28*Math.sin(t*.9+Math.trunc(key/8));
  return a;
}
function behaviorTail(f,column,anchor,p,t,key,r,h,speed,twist,length,fraction,night){
  const head=behaviorPath(f,column,anchor,p,t,key,r,h,twist,night), delay=Math.min(.28*length,p*6.5/Math.max(.01,speed))*fraction;
  const tail=behaviorPath(f,column,anchor,Math.max(0,p-delay*speed/6.5),t-delay,key,r,h,twist,night);
  const dx=tail.x-head.x,dy=tail.y-head.y,dz=tail.z-head.z,d=Math.hypot(dx,dy,dz),cap=.22*length,k=d>cap&&d>0?cap/d:1;
  return {x:head.x+dx*k,y:head.y+dy*k,z:head.z+dz*k};
}
