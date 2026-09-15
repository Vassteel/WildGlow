const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
const template=fs.readFileSync(path.join(root,'previews/wildglow.fragment.html.in'),'utf8');
const hash=template.match(/const hash = .*?;/)[0];
// Hash contains internal semicolons, so use the complete line.
const hashLine=template.split('\n').find(l=>l.includes('const hash ='));
const position=template.slice(template.indexOf('    function position('),template.indexOf('    /*__BEHAVIOR__*/'));
const context=vm.createContext({});
vm.runInContext(hashLine+'\nconst clamp=n=>Math.max(0,Math.min(1,n)); const smooth=n=>{n=clamp(n);return n*n*(3-2*n)};\n'+position+'\n'+fs.readFileSync(path.join(root,'previews/behavior.js'),'utf8'),context);
let checks=0;
for(const f of JSON.parse(fs.readFileSync(path.join(root,'previews/behavior-fixtures.json'),'utf8'))){
 const args=JSON.stringify([f.family,f.column,{x:1,y:-.3,z:.5},.43,12.4,17,.5,4]);
 const p=vm.runInContext('behaviorPath(...'+args+',1.45,'+f.night+')',context);
 const tail=vm.runInContext('behaviorTail(...'+args+',.9,1.45,2,1,'+f.night+')',context);
 for(const [actual,expected] of [[p,f.position],[tail,f.tail]])for(let i=0;i<3;i++){
   if(Math.abs(actual[['x','y','z'][i]]-expected[i])>0.00002)throw new Error('Preview differs from runtime: '+JSON.stringify(f)); checks++;
 }
}
console.log('PASS: '+checks+' preview/runtime trajectory coordinates agree.');
