# Unity Dependency Analyzer

Unity 6向けのEditor専用依存関係ビューアーです。現在ロードされているSceneを起点に、GameObject、Component、Inspector上のObject参照、Prefab Instanceとsource Prefabの関係をグラフ表示します。

現在の開発バージョンは`0.1.0-dev`です。変更内容は[CHANGELOG.md](CHANGELOG.md)を参照してください。

## Supported Analysis

- ロード済みSceneのroot/child GameObjectとHierarchy
- GameObjectに付与されたComponent
- Componentの`SerializedPropertyType.ObjectReference`
  - GameObject、Component、Asset、配列、nested field
  - 表示を省略する組み込みComponentの参照も解析し、edgeのsourceを所有GameObjectへ集約
  - `m_Script`やHierarchy/Prefab内部管理用propertyは除外
- Prefab Instance
  - ロード済みSceneの各nearest instance rootからsource Prefab assetへのedge
  - Instance上で実際に有効なserialized参照（overrideを含む）
  - nested Prefabの各instance rootと対応source Prefab
- Assetとsub-asset
  - GUIDとlocal file IDに基づき、同一path内の別sub-assetを別nodeとして識別
- Broken data
  - Missing Script
  - Missing Object Reference（Material、Meshなど）
  - 未設定の`None`はMissingとして扱わない
  - Component/property/追加scanner単位の失敗をIssue化し、取得済みの部分graphを保持して続行
- 現在のUnity Consoleに表示されるwarning/errorとの連携
  - ConsoleをClearして再解析すると、Consoleから消えたlogはIssues panelにも残らない
  - context objectやAsset pathから特定できるlogはgraph nodeへ関連付ける
  - nodeを特定できないlogも、クリック不能な`No related node`行としてIssues panelへ保持
  - `Editor.log`は解析対象にしない
  - Issues headerでConsole由来とAnalyzer独自のerror/warning件数を分けて表示

このツールが判定するのはserialized referenceとして確認できる一般的な事実です。「このAudioSourceにはAudioClipが必要」など、プロジェクト固有の用途や正しさは診断しません。

## Graph UI

- 通常依存は実線、Inspector参照は点線で表示
- `+`/`-`で通常子nodeを展開・収納
- `•••`からInspector参照を展開・収納
- node選択とUnityのHierarchy/Project/Inspector選択を同期
- 左クリックまたは中クリックのdragでpan、wheelでzoom
- minimap、parent jump、edge click、node highlight
- `Command + F` / `Ctrl + F`、Enter / Shift + Enter、arrow buttonによる検索移動
- node name、path、type、asset label、node kind、Missing状態を検索
- 下部`Issues` panelにMissing Referenceとscanner issueを表示
- node tooltipにはDependencies / Used Byを表示し、Asset Labelsはlabelを持つAssetだけに表示

toolbarの操作は次のとおりです。

- 初回は`Load`、完了後は`Reload`で再解析
- 実行中は進捗を表示し、buttonを無効化
- 公開されたCancel buttonはありません
- 検索結果だけへ絞る`Filter`処理は内部にありますが、現在のUIにはtoggleを提供していません

Hierarchy変更時は、開いているwindowの解析を自動更新します。

## Requirements

- Unity `6000.0.77f1`または`6000.4.5f1`
- Unity Editor
- UI Toolkit
- Tests/CIのみUnity Test Framework `1.6.0`

`UnityEditor.Experimental.GraphView`は使用せず、`VisualElement`ベースの独自graph UIを使用しています。

## Installation

このリポジトリの`DependencyAnalyzer` folderと`DependencyAnalyzer.meta`をUnity projectの`Assets`直下へコピーします。

```text
Assets/
└── DependencyAnalyzer/
    ├── Editor/
    └── Tests/
```

compile完了後、次のmenuからwindowを開きます。

```text
Tools > Dependency Analyzer > Open Graph
```

windowを開くと現在ロードされているSceneを自動解析します。必要に応じて`Reload`を実行してください。

共有設定を作る場合は、次を開いて`Create Shared Settings Asset`を実行します。

```text
Project Settings > Dependency Analyzer
```

共有設定は`Assets/DependencyAnalyzer/Editor/Settings/AnalyzerSettings.asset`に作成されます。

## Settings

- `Excluded Folders`: Asset参照node化から除外するfolder path
- `Excluded Extensions`: Asset参照node化から除外する拡張子
- `Scan Yield Batch Size`: scan中にEditorへ制御を返す間隔
- `Zoom Step`: graphのzoom感度
- `Initial Expansion Depth`: 初期表示の展開深度

除外設定は対象Asset参照だけに適用され、同じComponent内の他の参照は保持されます。

## Project Structure

```text
DependencyAnalyzer/
├── Editor/
│   ├── DependencyAnalyzer.Editor.asmdef
│   ├── DependencyGraphWindow.cs
│   ├── UnityTempDirectoryGuard.cs
│   ├── Controller/
│   │   └── Issues/
│   ├── Core/
│   ├── Scanners/
│   │   └── Issues/
│   ├── Settings/
│   ├── Tests/
│   │   ├── Controller/
│   │   ├── Core/
│   │   ├── Fixtures/
│   │   ├── Scanners/
│   │   └── UI/
│   ├── UI/
│   │   ├── Controls/
│   │   ├── GraphView/
│   │   ├── Icons/
│   │   └── Styles/
└── Tests/
    └── Runtime/
```

- `Core`: dependency graph/node/edge/issueとnode cache
- `Scanners`: Scene、serialized reference、Asset、Console issueの収集とnode生成
- `Controller`: scan orchestration、状態、検索、selection sync、Issues panel
- `UI`: UI Toolkitによるgraph/node/edge/toolbar/panel
- `Editor/Tests`: production責務別のEdit Mode Testと壊れたPrefab Fixture
- `Tests/Runtime`: GameObjectへattachするテスト専用Component assembly

production codeは`DependencyAnalyzer.Editor.asmdef`に分離され、Runtime buildには含まれません。`DependencyAnalyzer.TestFixtures`はUnity Test FrameworkのTest Assemblyとしてのみ利用します。

## Automated Verification

Edit Mode Testは、次の一般的な依存関係事実を検証します。

- Scene/Hierarchy/ComponentとInspector参照
- `None`と`Missing`の区別
- Renderer Material、配列、nested field、循環・重複参照
- inactive GameObject、disabled Component、additive Scene
- Prefab source、override、nested Prefab
- main asset path内のsub-asset identity
- Missing Script、Missing Object、Missing Material
- component/scanner失敗後の継続と部分結果保持
- Missing nodeのIssues panel登録
- 現在のUnity Console snapshot、Console Clear後の再解析、node未特定log、重複log
- node tooltipのAsset Labels表示条件、reference count維持、count badge非表示

通常fixtureはtest中に生成して削除します。コードだけで安定再現しにくいMissing状態は、`Editor/Tests/Fixtures`の小さなPrefab YAMLと固定`.meta`で保持します。

GitHub ActionsはAssets-copy導入を再現する最小`TestProject`を作り、Unity `6000.0.77f1`と`6000.4.5f1`のEdit Mode Testを実行します。名前付きユーザーライセンスでCIを動かすには、repository secrets `UNITY_EMAIL`と`UNITY_PASSWORD`が必要です。`UNITY_LICENSE`と`UNITY_SERIAL`は使用しません。

## Current Limitations

- 対象は現在ロード済みSceneから到達できる参照です。未ロードSceneや未使用Asset全体はscanしません。
- Prefab asset内部を未ロードのまま横断scanしません。対応範囲はScene上のinstance root/source assetと、instance上で有効なserialized参照です。
- Prefab source componentとのproperty単位の対応表は作りません。
- Addressables、`Resources.Load`、独自文字列IDなどの非serialized参照は対象外です。
- Package内scriptなど、表示policyから外れるComponentはnodeを省略する場合があります。serialized参照自体は所有GameObjectをsourceとして解析します。
- Console連携はUnity内部APIをreflectionで読み取ります。内部API取得に失敗した場合は、原因を`Unity Console Reader` Issueとして表示します。
- Console件数は現在のConsole snapshotに含まれるwarning/error行数です。Collapseが有効な場合は1表示行を1件とし、発生回数は`Occurrences`として詳細へ保持します。Missing Reference、scanner issue、Console Reader failureはAnalyzer件数へ含めます。
- 検索`Filter` toggleとscanのCancel buttonはUI未提供です。
- UPM package化と`package.json`追加は行っていません。配布方式はAssets folder copyです。

## Distribution Status

全Unity assetの`.meta`、最小Test Project、Edit Mode CI、CHANGELOGはrepositoryに含まれます。

ScreenshotとLicense本文は未提供です。License本文が追加・承認されるまで、release/tag作成やAsset Store提出を行わないでください。
