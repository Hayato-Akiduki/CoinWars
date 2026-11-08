using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoinWars;

public partial class GameWindow
{
    int comparisonShop, comparisonSlot;

    void CycleTextSize()
    {
        int next = preferences.TextPercent == 100 ? 115 : preferences.TextPercent == 115 ? 130 : 100;
        preferences = preferences with { TextPercent = next };
        textScale = next / 100.0;
        SavePreferences();
        // Update existing visuals without rebuilding the battle or interrupting its animations.
        void Resize(DependencyObject parent)
        {
            if (parent is TextBlock { Tag: double original } text)
            {
                text.FontSize = original * textScale;
                if(text.ToolTip is TextBlock tooltip) tooltip.FontSize=16*textScale;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) Resize(VisualTreeHelper.GetChild(parent,i));
        }
        Resize(scene);
        RenderOverlays();
    }

    void OpenComparison(int shopIndex)
    {
        if (!Planning || game.S.Shop[shopIndex] == null) return;
        comparisonShop = shopIndex;
        var preview=game.PreviewPurchase(shopIndex);
        comparisonSlot = preview.ResultBoardSlot ?? (game.Count < game.Capacity ? Array.FindIndex(game.S.Board,u => u == null) : Array.FindIndex(game.S.Board,u => u != null));
        SetDialog("compare");
    }

    static string Role(UnitDef d) => d.Skill == "heal" ? "回復役" : d.Skill == "guard" ? "防御役" : d.Skill is "gold" or "bigmoney" ? "収益・攻撃役" : d.Skill == "aoe" ? "全体攻撃役" : "単体攻撃役";
    static string SkillExplanation(string skill) => skill switch
    {
        "strike" => "敵1体へ攻撃力の2.2倍のダメージ。",
        "aoe" => "敵全体へ、それぞれ攻撃力の0.8倍のダメージ。",
        "heal" => "HP割合が最も低い味方を、攻撃力の3.5倍回復。",
        "guard" => "自分に最大HPの25%のシールドを付与し、通常攻撃。",
        "gold" => "攻撃力の1.4倍の攻撃に加えて、1コイン獲得。",
        "bigmoney" => "攻撃倍率は1.5＋所持コイン÷15。貯金が多いほど強力。",
        _ => Data.Skills[skill]
    };

    void ComparisonDialog()
    {
        string? key = game.S.Shop[comparisonShop];
        if (key == null) { SetDialog(null); return; }
        var purchased=game.PreviewPurchase(comparisonShop);
        var preview=game.PreviewPurchase(comparisonShop,comparisonSlot);
        var candidate = preview.Candidate;
        var d = Data.Def(key);
        var panel = Modal("キャラクター比較", "購入前の確認です。編成の入れ替えは行いません。",1120,800);
        panel.Children.Add(Label("比較する配置枠",18,White,true));
        var choices = new ComboBox { FontSize = 18 * textScale, MinHeight = 42, Margin = new Thickness(0,8,0,14) };
        for (int i=0;i<game.S.Board.Length;i++)
        {
            var u=game.S.Board[i];
            if (!purchased.CanPlaceAt(i,game.Capacity)) continue;
            choices.Items.Add(new ComboBoxItem { Tag=i,Content=$"{(i<4?"前衛":"後衛")} {i%4+1}：{(u==null?"空き枠へ追加":Data.Def(u).Name+" "+new string('★',u.Star))}",IsSelected=i==comparisonSlot });
        }
        choices.SelectionChanged += (_,_) => { if(choices.SelectedItem is ComboBoxItem {Tag:int slot} && slot!=comparisonSlot) { comparisonSlot=slot;RenderOverlays(); } };
        panel.Children.Add(choices);
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition());columns.ColumnDefinitions.Add(new ColumnDefinition());
        void AddCard(Unit? u,int column,string title)
        {
            var stack=new StackPanel {Margin=new Thickness(10)};
            stack.Children.Add(Label(title,18,Gold,true));
            if(u==null) stack.Children.Add(Label("空き枠",22,Muted));
            else
            {
                var def=Data.Def(u);var portrait=Portrait(u.Key);portrait.Height=110;portrait.Width=110;portrait.HorizontalAlignment=HorizontalAlignment.Left;stack.Children.Add(portrait);
                stack.Children.Add(Label(def.Name+" "+new string('★',u.Star),22,White,true));
                stack.Children.Add(Label($"{Role(def)} / {(def.Range==1?"近接・前衛向き":"遠隔・後衛向き")}",17,Cyan));
                stack.Children.Add(Label($"HP {def.Hp*Data.Mult(u.Star):0}　攻撃 {def.Atk*Data.Mult(u.Star):0}　速度 {def.Spd:0}",17,White));
                stack.Children.Add(Label("スキル："+SkillExplanation(def.Skill),17,White));
                stack.Children.Add(Label("絆："+string.Join("・",def.Traits.Select(t=>Data.Traits[t])),17,Muted));
            }
            Grid.SetColumn(stack,column);columns.Children.Add(stack);
        }
        AddCard(game.S.Board[comparisonSlot],0,"購入前の配置");AddCard(candidate,1,$"購入後 {(preview.Merges?"合成結果":"候補")} / {d.Cost} コイン");
        panel.Children.Add(columns);
        if(game.S.Board[comparisonSlot] is {} current)
        {
            var old=Data.Def(current);double mult=Data.Mult(current.Star);
            double candidateMult=Data.Mult(candidate.Star);
            panel.Children.Add(Label($"購入後との差：HP {d.Hp*candidateMult-old.Hp*mult:+0;-0;0}　攻撃 {d.Atk*candidateMult-old.Atk*mult:+0;-0;0}　速度 {d.Spd-old.Spd:+0;-0;0}",17,White));
        }
        panel.Children.Add(Label("数値は★を反映した基礎値です。装備・絆・投資の補正は含みません。",15,Muted));
        if(preview.Merges)panel.Children.Add(Label($"購入すると★{candidate.Star}へ合成。素材になるキャラが減ることも絆予測に反映しています。",17,Cyan));
        var predicted=preview.Board;
        var before=game.Counts(game.S.Board);var after=game.Counts(predicted);
        panel.Children.Add(Label("この枠に配置した場合の絆",20,Gold,true));
        foreach(var trait in Data.Traits.Keys.Where(t=>before[t]!=after[t]||d.Traits.Contains(t)))
        {
            int oldTier=game.Tier(trait,before),newTier=game.Tier(trait,after);
            string change=newTier>oldTier?"ボーナス強化":newTier<oldTier?"ボーナス低下":"段階は変わりません";
            panel.Children.Add(Label($"{Data.Traits[trait]}：{before[trait]} → {after[trait]}　{change}（必要数 {string.Join(" / ",Data.Thresholds[trait])}）",17,newTier<oldTier?B("#F4A4AD"):newTier>oldTier?Cyan:White));
        }
        panel.Children.Add(Label("同じ種類を複数置いても絆の人数は増えません。",15,Muted));
        int copies=game.Owned().Count(u=>u.Key==key&&u.Star==1);
        panel.Children.Add(Label($"★1の所持数：{copies}体"+(copies>=2?" / 購入で合成対象になります。":""),17,White));
        string purchaseError=game.PurchaseError(comparisonShop);
        if(purchaseError.Length>0)panel.Children.Add(Label(purchaseError,18,B("#F4A4AD")));
        var buy=Btn($"{d.Cost} コインで購入（所持 {game.S.Gold}）",()=>{SetDialog(null);Buy(comparisonShop);},Planning&&purchaseError.Length==0,true);
        buy.Height=48;buy.Margin=new Thickness(0,14,0,8);panel.Children.Add(buy);
        panel.Children.Add(Btn("閉じる [Esc]",()=>SetDialog(null)));
    }
}
