// Offline DOM harness: exercises the shipped script, not a browser or pixel renderer.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
const html=fs.readFileSync(path.join(root,'previews/index.html'),'utf8');
assert.equal(html,fs.readFileSync(path.join(root,'previews/wildglow.template.html'),'utf8'));
assert.ok(html.startsWith('<!doctype html>'));
assert.ok(!html.includes('/*__'));
const nodes=new Map(),downloads=[];
const context2d=new Proxy({}, {get(t,k){if(k in t)return t[k];if(k==='createRadialGradient'||k==='createLinearGradient')return ()=>({addColorStop(){}});return ()=>{}},set(t,k,v){t[k]=v;return true}});
class Element{
 constructor(tag){this.tag=tag;this.children=[];this.events={};this.value='';this.checked=true;this.isConnected=true;this.width=0;this.height=0;}
 addEventListener(name,fn){this.events[name]=fn}
 setAttribute(k,v){this[k]=v}
 appendChild(n){this.children.push(n)}
 append(...n){this.children.push(...n)}
 getBoundingClientRect(){return {width:500,height:400}}
 getContext(){return context2d}
 scrollIntoView(){}
 click(){if(this.tag==='a')downloads.push(decodeURIComponent(this.href.split(',').slice(1).join(',')));else this.events.click?.({target:this})}
 querySelector(s){if(!nodes.has(s))nodes.set(s,new Element(s));return nodes.get(s)}
}
const element=new Element('root');
const ctx=vm.createContext({document:{getElementById:()=>element,createElement:t=>new Element(t)},window:{},matchMedia:()=>({matches:false}),devicePixelRatio:1,IntersectionObserver:class{observe(){}disconnect(){}},ResizeObserver:class{observe(){}},Image:class{},performance:{now:()=>0},requestAnimationFrame(){},console});
vm.runInContext(html.match(/<script>([\s\S]*?)<\/script>/)[1],ctx);
const get=s=>element.querySelector(s);
assert.equal(get('[data-effect]').children.length,58);
assert.equal(get('.wg-gallery').children.length,58);
const picker=get('[data-effect]'),toggle=get('[data-enabled]');
for(let i=0;i<58;i++){
 picker.value=String(i);picker.events.change({target:picker});
 toggle.checked=false;toggle.events.change({target:toggle});
 assert.match(get('[data-meta]').textContent,/Disabled.*0 motes/);
}
get('[data-export]').click();
assert.equal((downloads.at(-1).match(/Enabled = false/g)||[]).length,58);
assert.equal((downloads.at(-1).match(/LightSpill = 1/g)||[]).length,58);
assert.match(downloads.at(-1),/\[Lighting\]\nSpillIntensity = 0.6/);
assert.match(downloads.at(-1),/\[Style.mushroom\]\nEnabled = false\nSurfaceGlow = 1.25/);
assert.match(downloads.at(-1),/\[Style.copper\]\nEnabled = false\nSurfaceGlow = 0.085/);
picker.value='0';picker.events.change({target:picker});assert.equal(toggle.checked,false);
toggle.checked=true;toggle.events.change({target:toggle});get('[data-export]').click();
assert.equal((downloads.at(-1).match(/Enabled = true/g)||[]).length,1);
assert.equal((downloads.at(-1).match(/Enabled = false/g)||[]).length,57);
const day=get('[data-scene]');day.value='day';day.events.change({target:day});assert.equal(get('[data-variant]').textContent,'Day');assert.equal(get('[data-other-variant]').textContent,'Night');
get('[data-play]').click();assert.equal(get('[data-play]').textContent,'Play motion');
console.log('PASS: both entry points embed all assets; 58 selectors, 58 independent on/off exports, day/night and pause controls. DOM harness only; no pixel validation.');
