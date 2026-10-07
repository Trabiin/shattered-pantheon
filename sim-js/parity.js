// Prints one line per fight (same teams, stages and seeds as `BattleSim --parity`), so the
// JavaScript and C# engines can be compared fight for fight. Usage: node parity.js [teams]
'use strict';
const fs=require('fs'),path=require('path');
const {runBattle}=require('./engine');
const load=f=>JSON.parse(fs.readFileSync(path.join(__dirname,'..','Unity','Assets','Resources','BattleData',f),'utf8'));
const data={rules:load('rules.json'),heroes:load('heroes.json'),enemies:load('enemies.json'),stages:load('stages.json')};
function place(ids){
  const hs=ids.map(id=>data.heroes.find(h=>h.id===id));
  const frontPref=hs.filter(h=>['Tank','Warrior'].includes(h.role)&&!h.backline).sort((a,b)=>(a.role==='Tank'?0:1)-(b.role==='Tank'?0:1));
  const front=frontPref.slice(0,2);
  const rest=hs.filter(h=>!front.includes(h));
  while(front.length<2&&rest.length>3)front.push(rest.shift());
  return [...front.map((h,i)=>({id:h.id,row:'front',slot:'front'+i})),...rest.map((h,i)=>({id:h.id,row:'back',slot:'back'+i}))];
}
const ids=data.heroes.map(h=>h.id);
const rnd=(()=>{let s=12345;return()=>{s=(s*1103515245+12345)%2147483648;return s/2147483648;};})();
const sampleTeam=()=>{const a=[...ids];for(let i=a.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[a[i],a[j]]=[a[j],a[i]];}return a.slice(0,5);};
const teams=Array.from({length:+(process.argv[2]||300)},sampleTeam);
const out=[];
for(const st of data.stages)teams.forEach((t,i)=>{
  const seed=1000+i*31,r=runBattle(data,place(t),st.id,seed);
  const hs=r.heroes.map(h=>`${h.id}:${h.dmg}/${h.heal}/${h.taken}/${h.diedAt==null?'-':h.diedAt}`).join(';');
  out.push(`${st.id}|${seed}|${r.result}|${r.actions}|${r.rituals}|${hs}`);
});
console.log(out.join('\n'));
