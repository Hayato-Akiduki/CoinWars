# CoinWars

キャラクターを雇用し、編成・装備・投資で20ラウンドの勝利を目指す、Windows向けのオートバトルゲームです。

## 友人と遊ぶ（1.3.0）

Windows向け配布ファイルは `CoinWars-Setup-v1.3.0.exe` です。[GitHub Releases](https://github.com/Hayato-Akiduki/CoinWars/releases/latest) からダウンロードできます。設定で同じ1～9桁のチャレンジ番号を入力し、「この番号で最初から挑戦」を押します。開始すると現在のプレイを置き換えます。同じバージョン・難易度・番号で開始条件が揃い、その後の選択によって展開が変わります。終了時の到達ラウンドとHPを比べてください。

購入直後は待機枠が金色に光ります。戦闘結果でMVPと最初に倒れた味方を確認できます。

- Windows x64向け。ランタイム同梱で、.NETの別途インストールは不要です。
- マウス中心の操作に対応。小画面では縦スクロールと2段ショップに切り替わります。
- 文字サイズ変更、一時停止・倍速、売却取り消し、合成結果の比較に対応。
- v1.1.1以前からは、今回のインストーラーを手動で実行してください。v1.2.0以降は起動時に更新を確認し、タイトル画面から更新できます。
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
