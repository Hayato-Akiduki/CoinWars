# コイン・ウォーズ

キャラクターを雇用し、編成・装備・投資で20ラウンドの勝利を目指す、Windows向けのオートバトルゲームです。

## ダウンロード

[Releases](https://github.com/Hayato-Akiduki/coin-wars/releases)から `CoinWars-Setup-v1.0.0.exe` をダウンロードして実行してください。

- Windows x64向け。ランタイム同梱で、.NETの別途インストールは不要です。
- マウス中心の操作に対応。小画面では縦スクロールと2段ショップに切り替わります。
- 文字サイズ変更、一時停止・倍速、売却取り消し、合成結果の比較に対応。
- セーブ先：`%LOCALAPPDATA%\CoinWars`。旧C#版のデータを引き継ぎます。
- インストーラーはコード署名されていません。

## 開発

Windows / .NET 10 SDKでビルドできます。

```powershell
dotnet run --project desktop/CoinWars.Checks -c Release
dotnet run --project desktop/CoinWars
```

インストーラー作成にはInno Setupが必要です。

```powershell
./desktop/build.ps1 -Compiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

[詳しい操作・更新履歴](desktop/README.md) / [ver1.0 リリースノート](desktop/RELEASE_NOTES_v1.0.0.md)

`money.html` は移植前のブラウザー版、`desktop/` はC#・WPF版です。

## 本作について

『崩壊：スターレイル』のマネーウォーズを参考に制作した非公式の独立したゲームです。公式作品・公式配布物ではありません。
キャラクター画像はこのプロジェクト用の生成素材です。[素材情報](desktop/CoinWars/Assets/GENERATION.md)
