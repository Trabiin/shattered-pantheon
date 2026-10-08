// Battle simulator. Usage:
//   node sim.js                     -> full balance report (random teams + reference teams), writes reports/latest.md
//   node sim.js --team hilde,solenne,thessaly,maren,pip --stage saint --runs 500
'use strict';
const fs=require('fs'),path=require('path');
const {runBattle}=require('./engine');
const load=f=>JSON.parse(fs.readFileSync(path.join(__dirname,'..','Unity','Assets','Resources','BattleData',f),'utf8'));
const data={rules:load('rules.json'),heroes:load('heroes.json'),enemies:load('enemies.json'),stages:load('stages.json')};
const args=Object.fromEntries(process.argv.slice(2).reduce((a,v,i,arr)=>{if(v.startsWith('--'))a.push([v.slice(2),arr[i+1]]);return a;},[]));

// Auto-placement: tanks and melee in front, everyone else behind.
function place(ids){
  const hs=ids.map(id=>data.heroes.find(h=>h.id===id));
  const frontPref=hs.filter(h=>['Tank','Warrior'].includes(h.role)&&!h.backline).sort((a,b)=>(a.role==='Tank'?0:1)-(b.role==='Tank'?0:1));
  const front=frontPref.slice(0,2);
  const rest=hs.filter(h=>!front.includes(h));
  while(front.length<2&&rest.length>3)front.push(rest.shift());
  return [...front.map((h,i)=>({id:h.id,row:'front',slot:'front'+i})),...rest.map((h,i)=>({id:h.id,row:'back',slot:'back'+i}))];
}
function runMany(ids,stageId,runs,seedBase=1){
  const team=place(ids);let wins=0,secs=0,winSecs=0,timeouts=0;
  for(let i=0;i<runs;i++){const r=runBattle(data,team,stageId,seedBase+i*7919);secs+=r.seconds;if(r.result==='win'){wins++;winSecs+=r.seconds;}if(r.result==='timeout')timeouts++;}
  return {winRate:wins/runs,avgSeconds:secs/runs,avgWinSeconds:wins?winSecs/wins:null,timeoutRate:timeouts/runs};
}
const pct=x=>(x*100).toFixed(0)+'%';
const mmss=s=>{if(s==null)return "n/a";s=Math.round(s);return `${Math.floor(s/60)}:${String(s%60).padStart(2,"0")}`;};

if(args.team){
  const r=runMany(args.team.split(','),args.stage||'saint',+(args.runs||300));
  console.log(`Team ${args.team} vs ${args.stage||'saint'}: win ${pct(r.winRate)}, avg fight ${mmss(r.avgSeconds)}, avg win ${mmss(r.avgWinSeconds)}, timeouts ${pct(r.timeoutRate)}`);
  process.exit(0);
}

// Full report
const TEAMS=+(args.teams||400), RUNS=+(args.runs||12);
const ids=data.heroes.map(h=>h.id);
const rnd=(()=>{let s=12345;return()=>{s=(s*1103515245+12345)%2147483648;return s/2147483648;};})();
const sampleTeam=()=>{const a=[...ids];for(let i=a.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[a[i],a[j]]=[a[j],a[i]];}return a.slice(0,5);};
const teams=Array.from({length:TEAMS},sampleTeam);
const REF={
  'Balanced (Hilde, Solenne, Thessaly, Maren, Pip)':['hilde','solenne','thessaly','maren','pip'],
  'All damage (Solenne, Varkhul, Thessaly, Ysolde, Grub)':['solenne','varkhul','thessaly','ysolde','grub'],
  'No control (Hilde, Varkhul, Ysolde, Maren, Grub)':['hilde','varkhul','ysolde','maren','grub'],
  'Double control (Hilde, Solenne, Thessaly, Seraphine, Pip)':['hilde','solenne','thessaly','seraphine','pip'],
};
let out=`# Battle Balance Report\n\n*Generated ${new Date().toISOString().slice(0,16).replace('T',' ')} UTC by \`node sim.js\`. ${TEAMS} random 5-hero teams × ${RUNS} fights per stage, auto-placed (tanks and melee in front). Fight length assumes ${data.rules.secondsPerActionAt1x}s per action at 1x.*\n\n`;
const flags=[];
for(const st of data.stages){
  const target=data.rules.difficultyTargetsSeconds[st.difficulty];
  const res=teams.map((t,i)=>({t,...runMany(t,st.id,RUNS,1000+i*31)}));
  const wr=res.map(r=>r.winRate).sort((a,b)=>a-b);
  const median=wr[Math.floor(wr.length/2)];
  const strong=res.filter(r=>r.winRate>=0.8).length/res.length;
  const winSecs=res.filter(r=>r.avgWinSeconds!=null).map(r=>r.avgWinSeconds).sort((a,b)=>a-b);
  const medWin=winSecs.length?winSecs[Math.floor(winSecs.length/2)]:null;
  out+=`## ${st.name} (${st.difficulty})\n\n`;
  out+=`| Measure | Value | Target |\n|---|---|---|\n`;
  out+=`| Random teams that win at least 80% | ${pct(strong)} | |\n| Median team win rate | ${pct(median)} | |\n`;
  out+=`| Median winning fight length | ${mmss(medWin)} | ${mmss(target[0])} to ${mmss(target[1])} |\n\n`;
  if(medWin!=null&&(medWin<target[0]||medWin>target[1]))flags.push(`**${st.name}:** winning fights take ${mmss(medWin)}, outside the ${st.difficulty} target of ${mmss(target[0])} to ${mmss(target[1])}.`);
  const cap=data.rules.reliableWinShareMax[st.difficulty];
  if(strong>cap)flags.push(`**${st.name}:** ${pct(strong)} of random teams win reliably; the ${st.difficulty} limit is ${pct(cap)}, so it may be too easy.`);
  if(strong<0.05)flags.push(`**${st.name}:** fewer than 5% of random teams reliably win. Fine for hard content only if well-built teams do (see reference teams).`);
  // hero lift
  const overall=res.reduce((a,r)=>a+r.winRate,0)/res.length;
  const lift=ids.map(id=>{const w=res.filter(r=>r.t.includes(id));const avg=w.reduce((a,r)=>a+r.winRate,0)/w.length;return {id,avg,lift:avg-overall};}).sort((a,b)=>b.lift-a.lift);
  out+=`Win rate of teams that include each hero (overall average ${pct(overall)}):\n\n| Hero | Win rate with hero | Difference |\n|---|---|---|\n`;
  lift.forEach(l=>{out+=`| ${data.heroes.find(h=>h.id===l.id).name} | ${pct(l.avg)} | ${l.lift>=0?'+':''}${(l.lift*100).toFixed(0)} pts |\n`;});
  out+='\n';
  if(lift[0].lift>0.25)flags.push(`**${st.name}:** ${data.heroes.find(h=>h.id===lift[0].id).name} looks like a must-have (+${(lift[0].lift*100).toFixed(0)} pts). Check that other answers exist.`);
  out+=`Reference teams (200 fights each):\n\n| Team | Win rate | Avg winning fight |\n|---|---|---|\n`;
  for(const [n,t] of Object.entries(REF)){const r=runMany(t,st.id,200,77);out+=`| ${n} | ${pct(r.winRate)} | ${mmss(r.avgWinSeconds)} |\n`;}
  out+='\n';
}
out=out.replace('# Battle Balance Report\n\n',`# Battle Balance Report\n\n`)+`## Flags\n\n${flags.length?flags.map(f=>'- '+f).join('\n'):'- None.'}\n`;
fs.writeFileSync(path.join(__dirname,'..','reports','latest.md'),out);
console.log(out);
