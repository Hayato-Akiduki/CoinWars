using System.IO;
using System.Text.Json;
namespace CoinWars;
public record UnitDef(string Key,string Name,int Cost,string[] Traits,double Hp,double Atk,double Spd,int Range,string Skill);
public record ItemDef(string Key,string Name,string Description);
public class Unit {
 public string Key {get;set;}="swordman"; public int Star {get;set;}=1;
 public List<string> Items {get;set;}=[]; public List<int> Equipped {get;set;}=[];
}
public record Offer(string Kind,string Title,string Description,string Argument="");
public enum Difficulty { Standard, Easy, Hard }

public record ScoreEntry(int Round, int Hp, bool Cleared, int? ChallengeNumber, Difficulty Difficulty = Difficulty.Standard)
{
 [System.Text.Json.Serialization.JsonIgnore]
 public int Points => Round * 1000 + Hp + (Cleared ? 1000 : 0);
}
public record BattleSave(int Round, string Outcome, int? Hp, int? Gold, int? HpBefore = null);
public record RunResult(int Attempt, string Outcome, int Round, int Hp, int Gold, int? ChallengeNumber, List<BattleSave> Battles, Difficulty Difficulty = Difficulty.Standard);
public class State {
 public Difficulty Difficulty {get;set;}
 public int? ChallengeNumber {get;set;}
 public uint ChallengeState {get;set;}
 public int Version {get;set;}=1;
 public int Plays {get;set;} public int Clears {get;set;} public int BestRound {get;set;} public int BestClearHp {get;set;}
 public List<ScoreEntry> Scores {get;set;}=[];
 public List<BattleSave> BattleSaves {get;set;}=[];
 public List<RunResult> RunResults {get;set;}=[];
 public ScoreEntry FinalScore() => new(Round, Hp, Round == 20 && Hp > 0 && Results.LastOrDefault() == "win", ChallengeNumber, Difficulty);
 public int Hp {get;set;}=100; public int Gold {get;set;}=6; public int Level {get;set;}=1; public int Exp {get;set;}
 public int Round {get;set;}=1; public int Streak {get;set;} public int InterestCap {get;set;}=5; public int Debt {get;set;}
 public bool Locked {get;set;} public string Phase {get;set;}="plan";
 public double AttackBuff {get;set;} public double HpBuff {get;set;}
 public Unit?[] Board {get;set;}=new Unit?[8]; public Unit?[] Bench {get;set;}=new Unit?[8]; public Unit?[] Enemy {get;set;}=new Unit?[8];
 public string?[] Shop {get;set;}=new string?[5]; public List<string> Items {get;set;}=[];
 public Dictionary<string,int> Emblems {get;set;}=[]; public List<string> Results {get;set;}=[]; public List<string> Log {get;set;}=[];
 public List<Offer> Offers {get;set;}=[];
}
public static class Data {
 public static readonly UnitDef[] Units=JsonSerializer.Deserialize<UnitDef[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"units.json")))!;
 public static readonly ItemDef[] Items=[
 new("blade","鋭い剣","攻撃+20%"),new("armor","重鎧","HP+25%"),new("boots","疾風の靴","速度+15%"),
 new("tome","魔導書","スキル+35%"),new("fang","吸血の牙","与ダメージの20%回復"),new("amulet","守りの護符","開幕シールド30%"),
 new("crystal","エネルギー結晶","開幕スキルゲージ+2"),new("purse","金貨袋","撃破時+1コイン")];
 public static readonly Dictionary<string,string> Traits=new(){["knight"]="騎士",["guard"]="守護",["mage"]="魔導",["wind"]="疾風",["rich"]="豪商"};
 public static readonly Dictionary<string,int[]> Thresholds=new(){["knight"]=[2,4],["guard"]=[2,3],["mage"]=[2,4],["wind"]=[2,3],["rich"]=[2,4,5]};
 public static readonly Dictionary<string,string> Skills=new(){["strike"]="単体2.2倍",["aoe"]="敵全体0.8倍",["heal"]="負傷した味方を回復",["guard"]="自己シールド",["gold"]="攻撃＋1コイン",["bigmoney"]="所持金に比例した攻撃"};
 public static readonly int[][] Odds=[[],[100,0,0,0,0],[100,0,0,0,0],[75,25,0,0,0],[55,30,15,0,0],[45,33,20,2,0],[30,40,25,5,0],[16,28,35,18,3],[12,18,32,28,10],[8,12,25,32,23]];
 public static readonly int[] Exp=[0,2,4,6,10,20,30,40,56,0];
 public static double Mult(int star)=>new[]{0,1,1.8,3.2}[star];
 public static UnitDef Def(Unit u)=>Def(u.Key);
 public static UnitDef Def(string key)=>Units.First(x=>x.Key==key);
}
public record CombatImpact(bool Player,int Slot,double Amount,string Kind);
public record BattleCue(bool Player,int Slot,string Skill,bool IsSkill,List<CombatImpact> Impacts);
public record MergeCue(string Key,int Star);
public record PurchasePreview(Unit Candidate, Unit?[] Board, bool Merges, int? ResultBoardSlot)
{
 public bool CanPlaceAt(int slot,int capacity)=>Board[slot]!=null||ResultBoardSlot.HasValue||Board.OfType<Unit>().Count()<capacity;
}
public class Game {
 string? saleBefore,saleAfter;
 public string SoldName {get;private set;}="";
 static string Snapshot(State state)=>JsonSerializer.Serialize(state);
 public bool CanUndoSale {
  get {if(saleBefore==null)return false;if(S.Phase!="plan"||saleAfter!=Snapshot(S)){saleBefore=null;saleAfter=null;return false;}return true;}
 }
 public bool UndoSale(){if(!CanUndoSale)return false;S=JsonSerializer.Deserialize<State>(saleBefore!)!;saleBefore=null;saleAfter=null;return true;}
 public BattleCue? LastCue {get;private set;}
 public MergeCue? LastMerge {get;private set;}
 readonly List<CombatImpact> impacts=[];
 public State S {get;set;}=new(); readonly Random rng;
 public List<Fighter> Fighters {get;private set;}=[]; public int Actions {get;private set;}
 public Game(int? seed=null){rng=seed.HasValue?new Random(seed.Value):new Random();}
 public string? FirstFallen {get;private set;}
 double NextFraction(){
  if(S.ChallengeNumber==null)return rng.NextDouble();
  uint value=S.ChallengeState;
  value^=value<<13; value^=value>>17; value^=value<<5;
  S.ChallengeState=value;
  return value/4294967296.0;
 }
 int NextIndex(int limit=int.MaxValue)=>S.ChallengeNumber==null?rng.Next(limit):(int)(NextFraction()*limit);
 public void New(int? challenge=null, Difficulty difficulty=Difficulty.Standard){
  if (!Enum.IsDefined(difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
  EndRun();
  FirstFallen=null;LastCue=null;LastMerge=null;
  S=new(){Difficulty=difficulty,ChallengeNumber=challenge,ChallengeState=(uint)(challenge??0)+1,Plays=S.Plays+1,Clears=S.Clears,BestRound=S.BestRound,BestClearHp=S.BestClearHp,Scores=S.Scores,RunResults=S.RunResults};
  Fighters=[];S.Board[1]=new(); Roll(); MakeEnemy(); Log("ユニットを購入し、選択→空きマスで配置。準備できたら戦闘開始。");
 }
 public void EndRun()
 {
  if (S.Plays == 0 || S.Phase is "over" or "battle") return;
  S.RunResults.Add(new(S.Plays, "abandoned", S.Round, S.Hp, S.Gold, S.ChallengeNumber, [..S.BattleSaves], S.Difficulty));
  S.Phase = "over";
 }
 public void Log(string s){S.Log.Insert(0,s);if(S.Log.Count>60)S.Log.RemoveAt(60);}
 T Pick<T>(IEnumerable<T> values){var a=values.ToArray();return a[NextIndex(a.Length)];}
 public IEnumerable<Unit> Owned()=>S.Board.Concat(S.Bench).OfType<Unit>();
 public int Count=>S.Board.OfType<Unit>().Count();
 public int Capacity=>Math.Min(8,S.Level);
 public int Interest=>Math.Min(S.InterestCap,S.Gold/10);
 public Dictionary<string,int> Counts(IEnumerable<Unit?> units,bool player=true){
  var counts=Data.Traits.Keys.ToDictionary(t=>t,_=>0);
  foreach(var u in units.OfType<Unit>().DistinctBy(u=>u.Key))foreach(var t in Data.Def(u).Traits)counts[t]++;
  if(player)foreach(var (t,n) in S.Emblems)counts[t]+=n;return counts;
 }
 public int Tier(string t,Dictionary<string,int>? counts=null)=>Data.Thresholds[t].Count(n=>(counts??Counts(S.Board))[t]>=n);
 string RandomUnit(int lv){int p=NextIndex(100),c=1;foreach(var chance in Data.Odds[lv]){p-=chance;if(p<0)break;c++;}return Pick(Data.Units.Where(u=>u.Cost==c)).Key;}
 public void Roll(){S.Shop=Enumerable.Range(0,5).Select(_=>(string?)RandomUnit(S.Level)).ToArray();}
 public string Reroll(){if(S.Phase!="plan")return "準備中のみ操作できます";if(S.Gold<2)return "コイン不足";S.Gold-=2;Roll();return "";}
 public string BuyExp(){if(S.Phase!="plan"||S.Level==9)return "レベルアップできません";if(S.Gold<4)return "コイン不足";S.Gold-=4;AddExp(4);return "";}
 public void AddExp(int n){S.Exp+=n;while(S.Level<9&&S.Exp>=Data.Exp[S.Level]){S.Exp-=Data.Exp[S.Level++];Log($"レベル{S.Level}に上昇");}if(S.Level==9)S.Exp=0;}
 public string PurchaseError(int index){
  if(S.Phase!="plan")return "準備中のみ購入できます";
  if(index<0||index>=S.Shop.Length||S.Shop[index] is not string key)return "購入済みです";
  int cost=Data.Def(key).Cost,slot=Array.IndexOf(S.Bench,null);
  if(S.Gold<cost)return "コイン不足";
  if(slot<0&&Owned().Count(u=>u.Key==key&&u.Star==1)<2)return "待機枠が満杯です";
  return "";
 }
 public string Buy(int index){
  LastMerge=null;
  string error=PurchaseError(index);if(error.Length>0)return error;
  string key=S.Shop[index]!;int cost=Data.Def(key).Cost,slot=Array.IndexOf(S.Bench,null);
  S.Gold-=cost;S.Shop[index]=null;
  if(slot>=0)S.Bench[slot]=new(){Key=key};else S.Bench=[..S.Bench,new Unit{Key=key}];
  Merge(key);S.Bench=S.Bench.Take(8).ToArray();return "";
 }
 public void Merge(string key){
  for(int star=1;star<=2;star++){
   var locs=new List<(Unit?[] a,int i)>();foreach(var a in new[]{S.Board,S.Bench})for(int i=0;i<a.Length;i++)if(a[i] is Unit u&&u.Key==key&&u.Star==star)locs.Add((a,i));
   if(locs.Count<3)continue;
   var items=locs.Take(3).SelectMany(l=>l.a[l.i]!.Items.Select((x,j)=>(key:x,round:l.a[l.i]!.Equipped.ElementAtOrDefault(j)))).ToArray();
   foreach(var (a,i) in locs.Take(3))a[i]=null;
   locs[0].a[locs[0].i]=new(){Key=key,Star=star+1,Items=items.Take(3).Select(x=>x.key).ToList(),Equipped=items.Take(3).Select(x=>x.round).ToList()};
   LastMerge=new(key,star+1);foreach(var x in items.Skip(3))AddItem(x.key);Log($"{Data.Def(key).Name} が ★{star+1} に合成！");
  }
 }
 public string Move(bool fromBench,int from,bool toBench,int to){
  if(S.Phase!="plan")return "";var a=fromBench?S.Bench:S.Board;var b=toBench?S.Bench:S.Board;
  if(a[from]==null)return "";
  if(fromBench&&!toBench&&b[to]==null&&Count>=Capacity)return $"配置上限は{Capacity}体です";
  (a[from],b[to])=(b[to],a[from]);return "";
 }
 public int SellValue(Unit u)=>Data.Def(u).Cost*(u.Star==1?1:u.Star==2?3:9)-(u.Star>1?1:0);
 public void Sell(bool bench,int i){if(S.Phase!="plan")return;var a=bench?S.Bench:S.Board;if(a[i] is not Unit u)return;
  saleBefore=Snapshot(S);SoldName=Data.Def(u).Name;
  S.Gold+=SellValue(u);foreach(var k in u.Items)AddItem(k);a[i]=null;saleAfter=Snapshot(S);
 }
 public PurchasePreview PreviewPurchase(int index,int? destination=null){
  string key=S.Shop[index]??throw new InvalidOperationException("購入済みです");
  var simulation=new Game(0){S=JsonSerializer.Deserialize<State>(Snapshot(S))!};
  simulation.S.Gold=Math.Max(simulation.S.Gold,Data.Def(key).Cost);
  var existing=simulation.Owned().ToHashSet();
  if(simulation.Buy(index).Length>0){var fallback=new Unit{Key=key};var board=simulation.S.Board.ToArray();if(destination is int slot)board[slot]=fallback;return new(fallback,board,false,null);}
  var candidate=simulation.Owned().Single(u=>!existing.Contains(u));
  int position=Array.IndexOf(simulation.S.Board,candidate);int? resultBoardSlot=position>=0?position:null;
  if(destination is int target){bool bench=position<0;int source=bench?Array.IndexOf(simulation.S.Bench,candidate):position;simulation.Move(bench,source,false,target);}
  return new(candidate,simulation.S.Board,simulation.LastMerge!=null,resultBoardSlot);
 }
 public void AddItem(string? key=null){if(S.Items.Count>=10){S.Gold+=2;Log("装備欄満杯：2コインに変換");}else S.Items.Add(key??Pick(Data.Items).Key);}
 public string Equip(Unit u,int i){if(S.Phase!="plan"||i<0||i>=S.Items.Count)return "";if(u.Items.Count>=3)return "装備は3個まで";u.Items.Add(S.Items[i]);u.Equipped.Add(S.Round);S.Items.RemoveAt(i);return "";}
 public string Unequip(Unit u,int i){if(S.Phase!="plan")return "";int wait=u.Equipped.ElementAtOrDefault(i)+2-S.Round;if(wait>0)return $"あと{wait}ラウンドで外せます";if(S.Items.Count>=10)return "装備欄が満杯です";S.Items.Add(u.Items[i]);u.Items.RemoveAt(i);u.Equipped.RemoveAt(i);return "";}
 public void MakeEnemy(){
  int r=S.Round;bool strong=r%5==0;int n=r==1?1:Math.Min(8,1+(int)Math.Ceiling(r/2.2)+(strong?1:0)),lv=Math.Min(9,1+(int)(r/2.3)+(strong?1:0));
  var units=new List<Unit>();for(int i=0;i<n;i++){double p=NextFraction()-(strong?.1:0);int star=r>=4&&p<Math.Min(.85,(r-3)*.09)?2:1;if(r>=13&&p<(r-12)*.04)star=3;
   var u=new Unit{Key=RandomUnit(lv),Star=star};if(r>=6)for(int j=0;j<r/6;j++)if(NextFraction()<.5){u.Items.Add(Pick(Data.Items.Where(x=>x.Key!="purse")).Key);u.Equipped.Add(0);}units.Add(u);}
  if(r==20){units[0]=new(){Key="tycoon",Star=3,Items=["blade","armor"],Equipped=[0,0]};units[1]=new(){Key="dragon",Star=2,Items=["tome"],Equipped=[0]};}
  S.Enemy=new Unit?[8];foreach(var u in units.OrderBy(u=>Data.Def(u).Range)){int[] order=Data.Def(u).Range==1?[1,2,0,3,5,6,4,7]:[5,6,4,7,1,2,0,3];S.Enemy[order.First(i=>S.Enemy[i]==null)]=u;}
 }
 public void CreateOffers(){
  string trait=Pick(Data.Traits.Keys),scout=Pick(Data.Units.Where(x=>x.Cost==2||x.Cost==3)).Key;
  Offer[] pool=[new("cash","臨時収入","+12コイン"),new("loan","高利貸し","+25コイン。次の5ラウンド、収入−6"),
  new("interest","投資家の心得","利子上限+3"),new("scout","スカウト",Data.Def(scout).Name+" ★2（満杯ならコイン）",scout),
  new("attack","鍛錬","味方全員の攻撃+10%・永続"),new("hp","鉄壁の訓練","味方全員のHP+15%・永続"),new("items","宝箱","装備を2個獲得"),
  new("heal","休息","プレイヤーHPを20回復"),new("exp","特訓","経験値+8"),new("emblem",Data.Traits[trait]+"の紋章","シナジー人数+1・永続",trait),
  new("gamble","ギャンブル","50%で所持金2倍、50%で半分")];
  S.Offers=pool.OrderBy(_=>NextIndex()).Take(3).ToList();S.Phase="event";
 }
 public void Choose(int i){
  if(S.Phase!="event"||i<0||i>=S.Offers.Count)return;var o=S.Offers[i];
  switch(o.Kind){case "cash":S.Gold+=12;break;case "loan":S.Gold+=25;S.Debt+=5;break;case "interest":S.InterestCap+=3;break;
   case "scout":int slot=Array.IndexOf(S.Bench,null);if(slot<0)S.Gold+=Data.Def(o.Argument).Cost*3;else{S.Bench[slot]=new(){Key=o.Argument,Star=2};Merge(o.Argument);}break;
   case "attack":S.AttackBuff+=.1;break;case "hp":S.HpBuff+=.15;break;case "items":AddItem();AddItem();break;case "heal":S.Hp=Math.Min(100,S.Hp+20);break;
   case "exp":AddExp(8);break;case "emblem":S.Emblems[o.Argument]=S.Emblems.GetValueOrDefault(o.Argument)+1;break;case "gamble":S.Gold=NextIndex(2)==0?S.Gold*2:S.Gold/2;break;}
  Log("イベント："+o.Title);S.Offers.Clear();S.Phase="plan";
 }
 public int Income(bool win,int? streak=null){int st=Math.Abs(streak??S.Streak);return 5+Interest+(st>=5?2:st>=3?1:0)+new[]{0,1,3,6}[Tier("rich")]+(win?1:0)-(S.Debt>0?6:0);}
 public bool Start(){if(S.Phase!="plan"||Count==0)return false;FirstFallen=null;Fighters=Build(S.Board,true).Concat(Build(S.Enemy,false)).ToList();Actions=0;S.Phase="battle";Log($"ラウンド {S.Round} 開戦");return true;}
 IEnumerable<Fighter> Build(Unit?[] board,bool player){
  var counts=Counts(board,player);int knight=Tier("knight",counts),guard=Tier("guard",counts),wind=Tier("wind",counts),mage=Tier("mage",counts),rich=Tier("rich",counts);
  for(int i=0;i<board.Length;i++){if(board[i] is not Unit u)continue;var d=Data.Def(u);bool Has(string t)=>d.Traits.Contains(t);int N(string k)=>u.Items.Count(x=>x==k);
   double hp=d.Hp*Data.Mult(u.Star)*(player?1+S.HpBuff:1),atk=d.Atk*Data.Mult(u.Star)*(player?1+S.AttackBuff:1),spd=d.Spd,sm=1;
   if(Has("knight"))atk*=new[]{1,1.15,1.4}[knight];if(Has("guard"))hp*=new[]{1,1.2,1.5}[guard];if(Has("wind"))spd*=new[]{1,1.2,1.5}[wind];if(Has("mage"))sm=new[]{1,1.15,1.35}[mage];
   if(Has("rich")&&rich>=2)atk*=1+Math.Min(.5,(player?S.Gold:20+S.Round*2)/100.0)*(rich==3?2:1);
   hp*=1+.25*N("armor");atk*=1+.2*N("blade");spd*=1+.15*N("boots");sm+=.35*N("tome");
   if (!player)
   {
    double strength = S.Difficulty switch { Difficulty.Easy => .8, Difficulty.Hard => 1.2, _ => 1 };
    hp *= strength;
    atk *= strength;
   }
   const int need = 3;
   yield return new(){Unit=u,Player=player,Slot=i,Hp=hp,MaxHp=hp,Attack=atk,Speed=spd,SkillMultiplier=sm,Reduction=Has("knight")&&knight==2?.15:0,
   Gauge=Has("wind")&&wind==2?100:NextFraction()*30,Shield=hp*(.3*N("amulet")+(guard==2?.15:0)),Need=need,Energy=Math.Min(need-1,2*N("crystal")),Steal=.2*N("fang"),Purse=N("purse")};
  }
 }
 void Hit(Fighter f,Fighter t,double mul){
  double dmg=Math.Round(f.Attack*mul*(.9+NextFraction()*.2)*(1-t.Reduction)),absorbed=Math.Min(t.Shield,dmg);t.Shield-=absorbed;t.Hp=Math.Max(0,t.Hp-dmg+absorbed);f.Dealt+=dmg;
  if(t.Player&&!t.Alive)FirstFallen??=Data.Def(t.Unit).Name;
  impacts.Add(new(t.Player,t.Slot,dmg,"damage"));
  f.Hp=Math.Min(f.MaxHp,f.Hp+dmg*f.Steal);if(t.Hp==0&&f.Player)S.Gold+=f.Purse;
 }
 public void Step(){
  if(S.Phase!="battle")return;impacts.Clear();var all=Fighters.Where(f=>f.Alive).ToArray();var ready=all.Where(f=>f.Gauge>=100).ToArray();
  if(ready.Length==0){double time=all.Min(f=>(100-f.Gauge)/f.Speed);foreach(var f in all)f.Gauge+=f.Speed*time;ready=all.Where(f=>f.Gauge>=99.999).ToArray();}
  var a=ready.OrderByDescending(f=>f.Gauge).First();a.Gauge-=100;a.Energy=Math.Min(a.Need,a.Energy+1);
  var foes=all.Where(f=>f.Player!=a.Player).ToArray();var allies=all.Where(f=>f.Player==a.Player).ToArray();var d=Data.Def(a.Unit);
  var front=foes.Where(f=>f.Slot<4).ToArray();var target=d.Range==1?(front.Length>0?front:foes).OrderBy(f=>Math.Abs(f.Slot%4-a.Slot%4)).First():Pick(foes);
  bool skill=a.Energy>=a.Need&&!(d.Skill=="heal"&&allies.All(f=>f.Hp>=f.MaxHp));if(skill)a.Energy=0;
  if(!skill)Hit(a,target,1);else switch(d.Skill){
   case "strike":Hit(a,target,2.2*a.SkillMultiplier);break;
   case "aoe":foreach(var t in foes)Hit(a,t,.8*a.SkillMultiplier);break;
   case "heal":var ally=allies.MinBy(f=>f.Hp/f.MaxHp)!;double healing=Math.Min(ally.MaxHp-ally.Hp,Math.Round(a.Attack*3.5*a.SkillMultiplier));ally.Hp+=healing;impacts.Add(new(ally.Player,ally.Slot,healing,"heal"));break;
   case "guard":double shield=Math.Round(a.MaxHp*.25*a.SkillMultiplier);a.Shield+=shield;impacts.Add(new(a.Player,a.Slot,shield,"shield"));Hit(a,target,1);break;
   case "gold":Hit(a,target,1.4*a.SkillMultiplier);if(a.Player)S.Gold++;break;
   case "bigmoney":Hit(a,target,(1.5+(a.Player?S.Gold:20+S.Round*2)/15.0)*a.SkillMultiplier);break;
  }
  LastCue=new(a.Player,a.Slot,d.Skill,skill,[..impacts]);
  Actions++;bool p=Fighters.Any(f=>f.Player&&f.Alive),e=Fighters.Any(f=>!f.Player&&f.Alive);
  if(!p||!e||Actions>240)End(!e&&p?"win":!p?"lose":"draw");
 }
 void End(string result){
  int hpBefore = S.Hp;
  bool win=result=="win",strong=S.Round%5==0;var mvp=Fighters.Where(f=>f.Player).MaxBy(f=>f.Dealt);Log("最初に倒れた味方："+(FirstFallen??"なし"));Log($"MVP：{Data.Def(mvp!.Unit).Name} / {mvp.Dealt:0}ダメージ");
  if(win){S.Streak=S.Streak>0?S.Streak+1:1;AddItem();if(strong){AddItem();S.Gold+=5;}Log("勝利！");}
  else {int damage=2+S.Round/3+Fighters.Where(f=>!f.Player&&f.Alive).Sum(f=>f.Unit.Star+Data.Def(f.Unit).Cost/2);if(result=="draw")damage=(int)Math.Ceiling(damage/2.0);
   S.Hp=Math.Max(0,S.Hp-damage);S.Streak=result=="draw"?0:S.Streak<0?S.Streak-1:-1;Log($"{(result=="draw"?"引き分け":"敗北")}：HP −{damage}");if(result=="lose"&&NextFraction()<.3)AddItem();}
  S.Results.Add(result);S.BestRound=Math.Max(S.BestRound,S.Round);
  if(S.Hp<=0||S.Round==20){
   var score = S.FinalScore();
   if(score.Cleared){S.Clears++;S.BestClearHp=Math.Max(S.BestClearHp,S.Hp);}
   S.Scores = S.Scores.Append(score).GroupBy(entry => entry.Difficulty)
       .SelectMany(group => group.OrderByDescending(entry => entry.Points).Take(10)).ToList();
   S.BattleSaves.Add(new(S.Round, result, S.Hp, S.Gold, hpBefore));
   S.RunResults.Add(new(S.Plays, score.Cleared ? "clear" : "defeat", S.Round, S.Hp, S.Gold, S.ChallengeNumber, [..S.BattleSaves], S.Difficulty));
   S.Phase="over";Log(score.Cleared?"全20ラウンド制覇！":"ゲーム終了");return;
  }
  int income=Income(win);S.Gold=Math.Max(0,S.Gold+income);if(S.Debt>0)S.Debt--;Log($"収入 {income:+0;-0;0} コイン");
  S.BattleSaves.Add(new(S.Round, result, S.Hp, S.Gold, hpBefore));
  S.Round++;AddExp(2);if(!S.Locked)Roll();MakeEnemy();S.Phase="plan";if(new[]{2,5,8,11,14,17}.Contains(S.Round))CreateOffers();
 }
}
public class Fighter {
 public Unit Unit {get;set;}=new(); public bool Player; public int Slot,Need,Energy,Purse;
 public double Hp,MaxHp,Attack,Speed,SkillMultiplier,Reduction,Gauge,Shield,Steal,Dealt; public bool Alive=>Hp>0;
}
public record SaveLoadResult(State? State, bool Recovered);
public static class Saves {
 public static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CoinWars");
 public static string FilePath=>Path.Combine(Folder,"save.json");
 public static void ArchiveInvalid(string path)
 {
  if(File.Exists(path)) File.Copy(path,path+".invalid-"+Guid.NewGuid().ToString("N"));
 }
 static bool Recoverable(Exception ex) => ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NullReferenceException or ArgumentException;
 static bool ValidFile(string path)
 {
  try { ReadSingle(path); return true; } catch(Exception ex) when(Recoverable(ex)) { return false; }
 }
 public static void Write(State s,string? path=null)
 {
  if(s.Phase=="battle")return;
  path??=FilePath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  string temp=path+".tmp";
  File.WriteAllText(temp,JsonSerializer.Serialize(s));
  // Replace only a validated primary into the backup slot. A corrupt primary
  // must never destroy the last valid recovery point.
  if(File.Exists(path)&&ValidFile(path)) File.Replace(temp,path,path+".bak");
  else
  {
   if(File.Exists(path)) ArchiveInvalid(path);
   File.Move(temp,path,true);
  }
 }
 public static State? Read(string? path=null) => ReadWithRecovery(path).State;
 public static SaveLoadResult ReadWithRecovery(string? path=null)
 {
  path??=FilePath;
  if(!File.Exists(path)&&!File.Exists(path+".bak"))return new(null,false);
  Exception? primaryError=null;
  try { return new(ReadSingle(path),false); }
  catch(Exception ex) when(Recoverable(ex)) { primaryError=ex; }
  try { return new(ReadSingle(path+".bak"),true); }
  catch(Exception ex) when(Recoverable(ex))
  {
   throw new InvalidDataException("セーブとバックアップを復元できませんでした。",new AggregateException(primaryError!,ex));
  }
 }
 static State ReadSingle(string path)
 {
  var s=JsonSerializer.Deserialize<State>(File.ReadAllText(path))??throw new InvalidDataException("空のセーブです");
  if(s.Board==null||s.Bench==null||s.Enemy==null||s.Shop==null||s.Items==null||s.Emblems==null||s.Results==null||s.Log==null||s.Offers==null)
   throw new InvalidDataException("セーブの必須項目がありません");
  if(!Enum.IsDefined(s.Difficulty)||s.Version!=1||s.Board.Length!=8||s.Bench.Length!=8||s.Enemy.Length!=8||s.Shop.Length!=5||s.Level<1||s.Level>9||s.Round<1||s.Round>20||s.Hp<0||s.Hp>100||s.Gold<0||s.Exp<0||s.InterestCap<0||s.Debt<0||!new[]{"plan","event","over"}.Contains(s.Phase))throw new InvalidDataException("セーブ形式が不正です");
  if(!s.Enemy.OfType<Unit>().Any()||!double.IsFinite(s.AttackBuff)||s.AttackBuff<0||!double.IsFinite(s.HpBuff)||s.HpBuff<0)throw new InvalidDataException("戦闘データが不正です");
  foreach(var u in s.Board.Concat(s.Bench).Concat(s.Enemy).OfType<Unit>())if(!Data.Units.Any(d=>d.Key==u.Key)||u.Star<1||u.Star>3||u.Items.Count>3||u.Items.Count!=u.Equipped.Count||u.Items.Any(k=>!Data.Items.Any(d=>d.Key==k)))throw new InvalidDataException("ユニットデータが不正です");
  if(s.Shop.Any(k=>k!=null&&!Data.Units.Any(d=>d.Key==k))||s.Items.Count>10||s.Items.Any(k=>!Data.Items.Any(d=>d.Key==k))||s.Emblems.Any(k=>!Data.Traits.ContainsKey(k.Key)||k.Value<0)||s.Board.OfType<Unit>().Count()>Math.Min(8,s.Level))throw new InvalidDataException("編成データが不正です");
  if(s.Phase=="event"&&(s.Offers.Count!=3||s.Offers.Any(o=>!new[]{"cash","loan","interest","scout","attack","hp","items","heal","exp","emblem","gamble"}.Contains(o.Kind)||(o.Kind=="scout"&&!Data.Units.Any(d=>d.Key==o.Argument))||(o.Kind=="emblem"&&!Data.Traits.ContainsKey(o.Argument)))))throw new InvalidDataException("イベントデータが不正です");
  if (s.Scores == null || s.Scores.GroupBy(entry => entry?.Difficulty).Any(group => group.Count() > 10) ||
      s.Scores.Any(entry => entry == null || !Enum.IsDefined(entry.Difficulty) || entry.Round is < 1 or > 20 || entry.Hp is < 0 or > 100 ||
          (entry.Cleared && (entry.Round != 20 || entry.Hp == 0))))
   throw new InvalidDataException("ランキングデータが不正です");
  if (s.BattleSaves == null || s.RunResults == null ||
      s.BattleSaves.Any(entry => entry == null) ||
      s.RunResults.Any(entry => entry?.Battles == null || !Enum.IsDefined(entry.Difficulty) || entry.Battles.Any(battle => battle == null)))
   throw new InvalidDataException("戦闘履歴が不正です");
  if (s.BattleSaves.Count == 0 && s.Results.Count > 0)
  {
   int firstRound = Math.Max(1, s.Round - s.Results.Count + (s.Phase == "over" ? 1 : 0));
   s.BattleSaves.AddRange(s.Results.Select((outcome, index) => new BattleSave(
       firstRound + index, outcome, null, null)));
  }
  s.Scores = s.Scores.OrderByDescending(entry => entry.Points).ToList();
  if (s.Phase == "over")
  {
   var score = s.FinalScore();
   var last = s.RunResults.LastOrDefault();
   if (last?.Attempt != s.Plays)
    s.RunResults.Add(new(s.Plays, score.Cleared ? "clear" : "defeat", s.Round, s.Hp, s.Gold, s.ChallengeNumber, [..s.BattleSaves], s.Difficulty));
   bool abandoned = last?.Attempt == s.Plays && last.Outcome == "abandoned";
   if (!s.Scores.Any(entry => entry.Difficulty == score.Difficulty) && !abandoned) s.Scores.Add(score);
  }
  return s;
 }
}
