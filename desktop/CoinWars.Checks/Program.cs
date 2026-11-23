using CoinWars;
using System.Text.Json;
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
var g=new Game(42);g.New();
Check(Data.Units.Length==16,"16 unit definitions");
g.S.Gold=100;g.S.Bench=Enumerable.Range(0,8).Select(_=>(Unit?)new Unit{Key="shield"}).ToArray();g.S.Bench[0]=new(){Key="archer"};g.S.Bench[1]=new(){Key="archer"};g.S.Shop[0]="archer";
Check(g.Buy(0)==""&&g.S.Bench.Length==8&&g.Owned().Any(u=>u.Key=="archer"&&u.Star==2),"full bench purchase merges");
g.New();g.S.Bench[0]=new(){Key="shield"};Check(g.Move(true,0,false,0)!=""&&g.Count==1,"board capacity enforced");Check(g.Move(true,0,false,1)==""&&g.S.Board[1]!.Key=="shield","swap at capacity");
g.S.Board[0]=new(){Key="shield"};Check(g.Counts(g.S.Board)["guard"]==1,"duplicate species do not stack synergy");
g.New();g.S.Items.Add("blade");var u=g.S.Board[1]!;g.Equip(u,0);Check(g.Unequip(u,0)!=""&&u.Items.Count==1,"equipment lock");g.S.Round=3;Check(g.Unequip(u,0)==""&&g.S.Items.Count==1,"equipment unlock");
g.S.Gold=59;Check(g.Interest==5,"interest cap");g.S.InterestCap=8;g.S.Gold=80;Check(g.Interest==8,"interest event scaling");
g.CreateOffers();string path=Path.Combine(Path.GetTempPath(),"coinwars-check-"+Guid.NewGuid()+".json");
Saves.Write(g.S,path);var loaded=Saves.Read(path)!;Check(JsonSerializer.Serialize(loaded.Offers)==JsonSerializer.Serialize(g.S.Offers),"event offers survive save");
g.S.Phase="battle";g.S.Gold=12345;Saves.Write(g.S,path);Check(Saves.Read(path)!.Gold!=12345,"battle preserves prebattle checkpoint");
File.Delete(path);
for(int seed=0;seed<100;seed++){
 var bot=new Game(seed);bot.New();int rounds=0;
 while(bot.S.Phase!="over"){
  if(bot.S.Phase=="event")bot.Choose(0);
  for(int i=0;i<5;i++)bot.Buy(i);
  if(bot.S.Gold>=12)bot.BuyExp();
  for(int i=0;i<8;i++)if(bot.S.Bench[i]!=null&&bot.Count<bot.Capacity){int pos=Array.IndexOf(bot.S.Board,null);bot.Move(true,i,false,pos);}
  var best=bot.Owned().OrderByDescending(x=>Data.Def(x).Cost*Data.Mult(x.Star)).Take(bot.Capacity).ToArray();
  bot.S.Board=new Unit?[8];bot.S.Bench=new Unit?[8];for(int i=0;i<best.Length;i++)bot.S.Board[i]=best[i];
  if(bot.Count==0)bot.S.Board[0]=new();
  foreach(var x in best)while(x.Items.Count<3&&bot.S.Items.Count>0)bot.Equip(x,0);
  if(!bot.Start())throw new Exception("start failed");
  int actions=0;while(bot.S.Phase=="battle"){bot.Step();if(++actions>242)throw new Exception("battle did not terminate");}
  if(++rounds>20||bot.S.Gold<0||bot.S.Hp<0)throw new Exception("run invariant");
 }
}
Check(true,"100 seeded full games terminate without invalid state");
g=new Game(123);g.New();g.S.Round=20;g.MakeEnemy();Check(g.S.Enemy.OfType<Unit>().Any(x=>x.Key=="tycoon"&&x.Star==3),"final boss");
g.Start();while(g.S.Phase=="battle")g.Step();Check(g.S.Phase=="over","round 20 ends run");
int bestRound=g.S.BestRound;g.New();Check(g.S.BestRound==bestRound&&g.S.Plays==2,"records survive new game");
g=new Game(93);g.New();g.Start();g.Step();
Check(g.LastCue is not null && g.LastCue.Impacts.Count>0,"battle action exposes animation cue");
Check(g.LastCue!.Impacts.All(x=>x.Amount>=0 && x.Slot>=0 && x.Slot<8),"animation impacts reference valid slots");
g.New();g.S.Gold=20;g.S.Bench[0]=new(){Key="archer"};g.S.Bench[1]=new(){Key="archer"};g.S.Shop[0]="archer";g.Buy(0);
Check(g.LastMerge is {Key:"archer",Star:2},"merge exposes rank-up cue");
g.S.Shop[1]="shield";g.Buy(1);Check(g.LastMerge is null,"later purchase clears old merge cue");
// Recovery uses real files to verify that a good backup survives subsequent writes.
string saveDir=Path.Combine(Path.GetTempPath(),"coinwars-recovery-"+Guid.NewGuid());
Directory.CreateDirectory(saveDir);
string recoveryPath=Path.Combine(saveDir,"save.json");
try {
 var recoveryGame=new Game(18);recoveryGame.New();recoveryGame.S.Gold=17;Saves.Write(recoveryGame.S,recoveryPath);
 recoveryGame.S.Gold=29;Saves.Write(recoveryGame.S,recoveryPath);
 Check(Saves.ReadWithRecovery(recoveryPath) is {Recovered:false,State.Gold:29},"valid primary takes precedence");
 string backup=File.ReadAllText(recoveryPath+".bak");
 File.WriteAllText(recoveryPath,"{broken");
 var recovered=Saves.ReadWithRecovery(recoveryPath);
 Check(recovered is {Recovered:true,State.Gold:17},"corrupt primary falls back to validated backup");
 Saves.Write(recovered.State!,recoveryPath);
 Check(Saves.Read(recoveryPath)!.Gold==17&&File.ReadAllText(recoveryPath+".bak")==backup,"recovery write preserves good backup");
 Check(Directory.GetFiles(saveDir,"*.invalid-*").Length==1,"corrupt primary is preserved for diagnosis");
 File.Delete(recoveryPath);
 Check(Saves.ReadWithRecovery(recoveryPath) is {Recovered:true,State.Gold:17},"missing primary uses backup");
 File.WriteAllText(recoveryPath,"{\"Version\":1,\"Board\":null}");
 Check(Saves.ReadWithRecovery(recoveryPath).Recovered,"structurally invalid primary uses backup");
 File.WriteAllText(recoveryPath+".bak","broken too");
 bool refused=false;try{Saves.ReadWithRecovery(recoveryPath);}catch(InvalidDataException){refused=true;}
 Check(refused,"both corrupt saves fail explicitly");
 Check(File.ReadAllText(recoveryPath+".bak")=="broken too","failed read never mutates saves");
} finally { Directory.Delete(saveDir,true); }
double real=0;
var playback=new PresentationTime(()=>real);
real=100;Check(playback.Milliseconds==100,"normal playback follows elapsed time");
playback.Speed=4;real=200;Check(playback.Milliseconds==500,"4x speed advances time fourfold");
playback.Suspended=true;real=1200;Check(playback.Milliseconds==500,"pause freezes pending presentation time");
playback.Speed=2;real=2200;Check(playback.Milliseconds==500,"speed change while paused cannot advance time");
playback.Suspended=false;real=2300;Check(playback.Milliseconds==700,"resume continues without catching up paused time");
Check(PresentationTime.ShouldSuspend(false,true),"dialog alone suspends battle");
Check(PresentationTime.ShouldSuspend(true,false),"closing dialog preserves manual pause");
Check(!PresentationTime.ShouldSuspend(false,false),"closing dialog resumes an unpaused battle");
var purchase=new Game(10);purchase.New();purchase.S.Gold=100;purchase.S.Level=9;
purchase.S.Bench=Enumerable.Range(0,8).Select(_=>(Unit?)new Unit{Key="shield",Star=3}).ToArray();purchase.S.Shop[0]="archer";
string unchanged=JsonSerializer.Serialize(purchase.S);
Check(purchase.PurchaseError(0)=="待機枠が満杯です"&&purchase.Buy(0)!=""&&JsonSerializer.Serialize(purchase.S)==unchanged,"full bench rejects purchase without spending");
purchase.S.Bench[0]=new(){Key="archer"};purchase.S.Bench[1]=new(){Key="archer"};
Check(purchase.PurchaseError(0)=="","full bench permits a merging purchase");
unchanged=JsonSerializer.Serialize(purchase.S);
var projected=purchase.PreviewPurchase(0,0);
Check(projected.Merges&&projected.Candidate.Star==2,"purchase preview includes rank two merge");
Check(JsonSerializer.Serialize(purchase.S)==unchanged,"purchase preview never mutates real state");
purchase.S.Gold=0;Check(purchase.PurchaseError(0)=="コイン不足"&&purchase.PreviewPurchase(0).Candidate.Star==2,"unaffordable merge can still be inspected");
var chain=new Game(11);chain.New();chain.S.Level=9;chain.S.Gold=100;chain.S.Board=new Unit?[8];chain.S.Bench=new Unit?[8];
chain.S.Board[0]=new(){Key="swordman",Items=["blade"],Equipped=[1]};chain.S.Board[1]=new(){Key="swordman"};
chain.S.Board[2]=new(){Key="swordman",Star=2};chain.S.Board[3]=new(){Key="swordman",Star=2};chain.S.Shop[0]="swordman";
var chained=chain.PreviewPurchase(0,0);
Check(chained.Candidate.Star==3&&chained.Candidate.Items.SequenceEqual(["blade"]),"preview includes chain merge and retained equipment");
Check(chained.Board.OfType<Unit>().Count()==1&&chain.Count==4,"preview removes consumed board units only in simulation");
var undo=new Game(12);undo.New();undo.S.Board[1]!.Items=["blade","tome"];undo.S.Board[1]!.Equipped=[1,1];undo.S.Items=Enumerable.Repeat("armor",10).ToList();
string beforeSale=JsonSerializer.Serialize(undo.S);undo.Sell(false,1);
Check(undo.CanUndoSale&&undo.S.Board[1]==null,"sale exposes undo");
Check(undo.UndoSale()&&JsonSerializer.Serialize(undo.S)==beforeSale,"undo restores unit equipment gold bag and overflow conversion");
Check(!undo.UndoSale()&&JsonSerializer.Serialize(undo.S)==beforeSale,"sale cannot be undone twice");
undo.Sell(false,1);undo.S.Gold=100;undo.S.Shop[0]="archer";undo.Buy(0);
Check(!undo.CanUndoSale&&!undo.UndoSale(),"subsequent purchase invalidates undo");
var phaseUndo=new Game(13);phaseUndo.New();phaseUndo.Sell(false,1);phaseUndo.S.Phase="battle";
Check(!phaseUndo.CanUndoSale,"undo is not allowed during battle");
var placement=new Game(14);placement.New();placement.S.Level=2;placement.S.Gold=100;placement.S.Shop[0]="archer";
var beforePlacement=placement.PreviewPurchase(0);
Check(beforePlacement.CanPlaceAt(0,placement.Capacity)&&beforePlacement.CanPlaceAt(2,placement.Capacity),"comparison keeps all empty slots when one capacity remains");
placement.S.Level=1;
Check(!placement.PreviewPurchase(0).CanPlaceAt(0,placement.Capacity)&&placement.PreviewPurchase(0).CanPlaceAt(1,placement.Capacity),"comparison at capacity permits swaps only");
Check(chained.CanPlaceAt(7,1),"merged board candidate can move to another empty slot");
// Compare prediction with actual purchases and moves across legal random inventories.
for(int seed=0;seed<500;seed++){
 var random=new Random(seed);var sample=new Game(seed);sample.New();sample.S.Level=9;sample.S.Gold=100;
 sample.S.Board=new Unit?[8];sample.S.Bench=new Unit?[8];
 foreach(var slots in new[]{sample.S.Board,sample.S.Bench})for(int i=0;i<8;i++){
  if(random.Next(4)==0)continue;
  string key=Data.Units[random.Next(Data.Units.Length)].Key;int star=random.Next(1,4);
  if(star<3&&sample.Owned().Count(x=>x.Key==key&&x.Star==star)>=2)continue;
  slots[i]=new(){Key=key,Star=star};
 }
 sample.S.Shop[0]=Data.Units[random.Next(Data.Units.Length)].Key;
 if(sample.PurchaseError(0).Length>0)continue;
 string original=JsonSerializer.Serialize(sample.S);
 for(int destination=0;destination<8;destination++){
  var prediction=sample.PreviewPurchase(0,destination);
  var actual=new Game(seed){S=JsonSerializer.Deserialize<State>(original)!};var owned=actual.Owned().ToHashSet();
  actual.Buy(0);var bought=actual.Owned().Single(x=>!owned.Contains(x));
  int source=Array.IndexOf(actual.S.Board,bought);bool fromBench=source<0;
  actual.Move(fromBench,fromBench?Array.IndexOf(actual.S.Bench,bought):source,false,destination);
  if(JsonSerializer.Serialize(prediction.Board)!=JsonSerializer.Serialize(actual.S.Board)||JsonSerializer.Serialize(prediction.Candidate)!=JsonSerializer.Serialize(bought))throw new Exception($"prediction mismatch seed {seed}");
 }
 if(JsonSerializer.Serialize(sample.S)!=original)throw new Exception("prediction changed source state");
}
Check(true,"500 randomized inventories predict real purchase and placement results");
string damagedDir=Path.Combine(Path.GetTempPath(),"coinwars-release-check-"+Guid.NewGuid());Directory.CreateDirectory(damagedDir);
try{
 string damagedPath=Path.Combine(damagedDir,"save.json");var valid=new Game(15);valid.New();Saves.Write(valid.S,damagedPath);Saves.Write(valid.S,damagedPath);
 valid.S.Enemy=new Unit?[8];File.WriteAllText(damagedPath,JsonSerializer.Serialize(valid.S));
 Check(Saves.ReadWithRecovery(damagedPath) is {Recovered:true},"empty enemy save recovers instead of crashing at battle start");
 valid.MakeEnemy();valid.S.HpBuff=-1;File.WriteAllText(damagedPath,JsonSerializer.Serialize(valid.S));
 Check(Saves.ReadWithRecovery(damagedPath) is {Recovered:true},"invalid combat buff recovers from backup");

 var ranked=new Game(16);ranked.New(123);
 ranked.S.Scores=Enumerable.Range(1,10).Select(round=>new ScoreEntry(round,100,false,null)).ToList();
 ranked.S.Round=20;
 ranked.MakeEnemy();
 ranked.Start();while(ranked.S.Phase=="battle")ranked.Step();
 var finalScore=ranked.S.FinalScore();
 Check(ranked.S.Scores.Count==10&&ranked.S.Scores[0]==finalScore&&ranked.S.Scores[^1].Round==2,"finished run enters ranked top ten and drops lowest score");
 string rankingPath=Path.Combine(damagedDir,"ranking.json");
 Saves.Write(ranked.S,rankingPath);
 ranked.S=Saves.Read(rankingPath)!;ranked.New();
 Check(ranked.S.Scores.Count==10&&ranked.S.Scores[0].ChallengeNumber==123,"leaderboard survives save and a new run");
 string legacyPath=Path.Combine(damagedDir,"legacy.json");
 var savedJson=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(rankingPath))!.AsObject();
 savedJson.Remove(nameof(State.Scores));
 File.WriteAllText(legacyPath,savedJson.ToJsonString());
 Check(Saves.Read(legacyPath)!.Scores.SequenceEqual([finalScore]),"legacy finished save imports the last result");

 var history=new Game(17);history.New(321);history.S.Hp=83;
 history.Start();while(history.S.Phase=="battle")history.Step();
 string historyPath=Path.Combine(damagedDir,"history.json");
 Saves.Write(history.S,historyPath);
 history.S=Saves.Read(historyPath)!;
 Check(history.S.BattleSaves.Count==1 && history.S.BattleSaves[0] is {Round:1,HpBefore:83,Hp:not null,Gold:not null} battle &&
       battle.Hp==history.S.Hp && battle.Outcome==history.S.Results[0],"completed battle saves HP before and after combat with the outcome and gold");
 var savedBattle=history.S.BattleSaves.Single();
 history.New();
 Check(history.S.BattleSaves.Count==0 && history.S.RunResults.Single() is {Outcome:"abandoned",ChallengeNumber:321} abandoned &&
       abandoned.Battles.SequenceEqual([savedBattle]),
       "restarting archives the previous challenge and clears the active battle list");
 history.EndRun();history.EndRun();
 Saves.Write(history.S,historyPath);
 history.S=Saves.Read(historyPath)!;
 Check(history.S.RunResults.Count==2 && history.S.Scores.Count==0,"ending a run twice or reloading does not duplicate results or assign a score");
 Check(Saves.Read(rankingPath)!.RunResults.Single().Battles.Single().Round==20,"naturally finished run archives the last battle");
}finally{Directory.Delete(damagedDir,true);}
Check(new ScoreEntry(20,1,true,null).Points>new ScoreEntry(20,100,false,null).Points,"a clear outranks any non-clear");
var challengeA=new Game();var challengeB=new Game();
challengeA.New(123456);challengeB.New(123456);
Check(JsonSerializer.Serialize(challengeA.S)==JsonSerializer.Serialize(challengeB.S),"same challenge number produces same opening");
var resumedChallenge=new Game() {S=JsonSerializer.Deserialize<State>(JsonSerializer.Serialize(challengeA.S))!};
challengeA.Roll();resumedChallenge.Roll();
Check(challengeA.S.Shop.SequenceEqual(resumedChallenge.S.Shop),"challenge draws resume after reload");
challengeA.Start();while(challengeA.S.Phase=="battle")challengeA.Step();
Check(challengeA.S.Log.Any(entry=>entry.StartsWith("最初に倒れた味方：")),"battle report records first casualty or no casualties");

byte[] installerBytes = [0x4d, 0x5a, 0x01, 0x02];
string installerHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(installerBytes));
string installerName = "CoinWars-Setup-v1.10.0.exe";
string ReleaseJson(string? digest, bool prerelease = false) => JsonSerializer.Serialize(new
{
 tag_name = "v1.10.0", draft = false, prerelease,
 assets = new[] { new { name = installerName, size = installerBytes.Length, digest } }
});
string releaseJson = ReleaseJson($"sha256:{installerHash}");
byte[] downloadBody = installerBytes;
Uri? downloadUrl = null;
using var updateHttp = new HttpClient(new UpdateHttpHandler(uri =>
{
 if (uri.Host == "api.github.com") return new StringContent(releaseJson);
 downloadUrl = uri;
 return new ByteArrayContent(downloadBody);
}));
var updater = new ReleaseUpdates(updateHttp);
var update = await updater.CheckAsync(new Version(1, 9, 0, 0), default);
Check(update?.Version == new Version(1, 10, 0), "updater compares release versions numerically");
Check(await updater.CheckAsync(new Version(1, 10, 0, 0), default) == null &&
      await updater.CheckAsync(new Version(2, 0, 0, 0), default) == null, "updater skips current and older versions");
releaseJson = ReleaseJson($"sha256:{installerHash}", prerelease: true);
Check(await updater.CheckAsync(new Version(1, 9, 0), default) == null, "updater skips prereleases");
releaseJson = ReleaseJson(null);
bool missingDigestRejected = false;
try { await updater.CheckAsync(new Version(1, 9, 0), default); }
catch (InvalidDataException) { missingDigestRejected = true; }
Check(missingDigestRejected, "updater rejects an installer without a published SHA-256");

string updateTestDir = Path.Combine(Path.GetTempPath(), "coinwars-update-check-" + Guid.NewGuid());
Directory.CreateDirectory(updateTestDir);
try
{
 string verified = Path.Combine(updateTestDir, "valid.download");
 await updater.DownloadAsync(update!, verified, default);
 Check(File.ReadAllBytes(verified).SequenceEqual(installerBytes) &&
       downloadUrl?.AbsoluteUri == $"https://github.com/Hayato-Akiduki/CoinWars/releases/download/v1.10.0/{installerName}",
       "updater verifies bytes from the expected release asset");
 downloadBody = [0x4d, 0x5a, 0xff, 0xff];
 bool tamperedRejected = false;
 try { await updater.DownloadAsync(update!, Path.Combine(updateTestDir, "bad.download"), default); }
 catch (InvalidDataException) { tamperedRejected = true; }
 Check(tamperedRejected, "updater rejects an installer whose bytes do not match the published SHA-256");
}
finally { Directory.Delete(updateTestDir, true); }
Console.WriteLine($"{checks} checks passed.");

sealed class UpdateHttpHandler(Func<Uri, HttpContent> respond) : HttpMessageHandler
{
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
     Task.FromResult(new HttpResponseMessage { Content = respond(request.RequestUri!) });
}
