# Unity Dependency Analyzer

Unity 6 向けのエディタ拡張です。現在ロードされているシーンの Hierarchy に存在する GameObject、表示対象 Component、Inspector 上の参照、Prefab 由来の参照などをグラフとして可視化し、依存関係の把握、Missing Reference の調査、不要な参照の発見を支援します。

このツールは Asset Store 配布品質を意識し、Model / View / Controller を分離した構成で実装しています。解析処理、データ保持、UI 描画を分けることで、Unity の UI Toolkit 変更や今後の解析ルール追加に対する影響範囲を小さくしています。

## Demo / Screenshots

> Add screenshots or GIFs here.

<!--
Example:

![Dependency graph demo](docs/images/dependency-graph-demo.png)
-->

## Features

- Unity 6 editor extension
  - `Editor` 配下のみで構成し、ゲーム本編の Runtime build には含めません。
  - `DependencyAnalyzer.Editor.asmdef` により、ツール用コードを独立した Editor Assembly として管理します。

- Loaded scene dependency graph
  - 現在ロードされているすべての Scene の Hierarchy に存在する Object を対象に可視化します。
  - 未使用アセット全体のスキャン結果はグラフに出さず、ロード済み Scene から到達できるものを中心に表示します。
  - ウィンドウを開いた後に Hierarchy が変更された場合は、自動で再スキャンします。

- Scene object and component analysis
  - GameObject 間の親子関係を抽出します。
  - 表示対象 Component をノードとして表示します。
  - Assets 配下の Script を持つ MonoBehaviour を表示対象にします。
  - Transform / RectTransform、MeshFilter / Renderer / Collider、テンプレート由来の Camera / Light / AudioSource など、冗長になりやすい組み込み Component は原則として省略します。
  - Renderer ノードを省略した場合でも、Material 参照は GameObject からの Inspector 参照として追加します。
  - Prefab instance は通常 Object と区別し、Prefab icon と専用色で表示します。

- Inspector reference analysis
  - SerializedProperty を使い、Inspector で参照されている Object、Component、Asset を抽出します。
  - Inspector 参照はデフォルトでは展開せず、ノード上のミートボールメニューから表示します。
  - 表示対象 Component の AudioClip、Material、SerializeField / public field なども Inspector 参照として扱います。

- Graph visualization
  - Hierarchy などの通常依存は実線 edge で表示します。
  - Inspector 参照は点線 edge で表示します。
  - edge は折れ線ではなく曲線で描画し、視認性を高めています。
  - 同じノードが複数箇所に現れる場合でも木構造として表示し、root に最も近いもの以外は初期状態で収納します。
  - 深い階層のノードは段階的に小さくし、depth 3 以降は縦方向に詰めて配置します。

- Node design
  - Object、Prefab、C# Script、Audio、Material、Camera、Canvas などをアイコンと色で区別します。
  - ノードは深い階層ほど小さく表示します。
  - フォントサイズは一定にし、小さいノードでも読みやすさを保ちます。
  - ノード本文にはパスを表示せず、ホバー時の tooltip に詳細情報を表示します。
  - tooltip には path、type、file size、labels、dependencies、used by を表示します。

- Lazy expansion
  - 初期表示では深さを制限し、必要な場所だけを `+` ボタンで展開します。
  - Inspector 参照は `+` では開かず、ミートボールメニューでのみ展開します。
  - Prefab instance / Prefab asset / AudioClip / Texture / Mesh など、グラフを大きくしやすいノードは初期状態で収納します。
  - `-` ボタンで対象ノード以下を収納できます。

- Editor synchronization
  - グラフ上のノードをクリックすると Unity の Hierarchy / Project で対象を Ping し、Inspector に表示します。
  - Unity 側で Object を選択した場合、対応するグラフノードへ視点を移動します。
  - edge や親方向ボタンからノードへジャンプした際は、短い黄色い縁のフラッシュで対象を示します。

- Navigation support
  - ノードのドラッグ移動に対応しています。
  - 左クリックまたは中クリックのドラッグで pan できます。
  - マウスホイールでカーソル位置を基準に zoom できます。
  - ズームステップは toolbar のスライダーで調整できます。
  - `Command + F` / `Ctrl + F` で検索欄へ移動し、Enter / Shift + Enter または `Next` / `Prev` で一致ノードを移動できます。
  - 検索は node name、path、type、asset label、Missing 状態、node kind を対象にします。
  - `Filter` を有効にすると、一致ノードとその親方向の文脈だけをグラフに表示します。
  - 右上の minimap からグラフ全体の位置を把握し、クリックした位置へ移動できます。
  - ズーム範囲は暴走しにくいように固定し、操作感だけを `Zoom Step` で調整します。

- Missing reference support
  - Missing Component や Missing Object Reference を検出します。
  - 非表示の子に Missing Reference がある場合は、最も近い可視親ノードへ警告を伝播します。
  - 下部の `Issues` panel に Missing Reference と Scanner issue を一覧表示し、クリックで該当ノードへ移動できます。

## Requirements

- Unity 6
- Editor only
- UI Toolkit
- UnityEditor.Experimental.GraphView は使用せず、独自の VisualElement ベース Graph UI を使用しています。

## Installation

1. このリポジトリの `DependencyAnalyzer` フォルダを Unity プロジェクトの `Assets` 配下へ配置します。

```text
Assets/
└── DependencyAnalyzer/
    └── Editor/
```

2. Unity Editor を開き、コンパイルが完了するまで待ちます。

3. メニューから以下を実行します。

```text
Tools > Dependency Analyzer > Open Graph
```

4. ウィンドウが開くと、現在ロードされている Scene を自動でスキャンします。必要に応じて `Scan` ボタンで再スキャンできます。

5. 設定を共有 asset として保存したい場合は、Project Settings から以下を開きます。

```text
Project Settings > Dependency Analyzer
```

必要に応じて `Create Shared Settings Asset` を押すと、`Assets/DependencyAnalyzer/Editor/Settings/AnalyzerSettings.asset` を作成します。

## Usage

- `Scan`
  - 現在ロードされている Scene の依存関係を再解析します。

- `Cancel`
  - 実行中のスキャンをキャンセルします。

- Hierarchy changes
  - ウィンドウが開いている間に Hierarchy が変更されると、自動で再スキャンします。

- `Zoom Step`
  - トラックパッドやマウスホイールのズーム感度を調整します。

- Search
  - `Command + F` / `Ctrl + F` で検索欄を focus します。
  - 入力中に一致数を表示し、現在の一致ノードを黄色い枠で強調します。
  - Enter / `Next` で次の一致、Shift + Enter / `Prev` で前の一致へ移動します。
  - Escape で検索語をクリアします。
  - `Filter` を有効にすると、一致ノードと親方向の文脈だけを表示します。
  - `name:Player`、`path:Assets/UI`、`type:Material`、`label:shared`、`kind:Asset`、`missing:true` のような絞り込み語も使えます。

- Issues
  - 下部 panel に Missing Reference と Scanner issue を表示します。
  - 行をクリックすると、対応する graph node へ移動します。
  - `Hide` / `Show` で一覧部分を折りたためます。

- Pan / Zoom
  - グラフ背景を左クリックまたは中クリックでドラッグすると pan できます。
  - マウスホイールで zoom できます。

- `+`
  - 通常の子ノードを展開します。

- `-`
  - 対象ノード以下の通常子ノードを収納します。

- `•••`
  - Inspector 参照を展開または収納します。
  - AudioClip、Material、SerializeField / public field などによる参照はここに格納されます。

- Parent jump button
  - ノード左側の親方向ボタンから親ノードへ移動します。

- Edge click
  - edge をクリックすると接続先の子ノードへ移動します。

## Settings

`Project Settings > Dependency Analyzer` では以下を設定できます。

- `Excluded Folders`
  - Asset 参照ノード化から除外する folder path です。

- `Excluded Extensions`
  - Asset 参照ノード化から除外する file extension です。

- `Scan Yield Batch Size`
  - スキャン中に Editor へ制御を返す間隔です。

- `Zoom Step`
  - GraphView の zoom 感度です。

`AnalyzerSettings` には `Initial Expansion Depth` も保持しており、初期表示の展開深度に使います。

## File Structure

```text
DependencyAnalyzer/
└── Editor/
    ├── DependencyAnalyzer.Editor.asmdef
    ├── DependencyWindow.cs
    ├── Controller/
    │   ├── DependencyGraphController.cs
    │   └── EditorSelectionSync.cs
    ├── Core/
    │   ├── DependencyCache.cs
    │   ├── DependencyEdgeData.cs
    │   ├── DependencyGraphData.cs
    │   ├── DependencyNodeData.cs
    │   └── DependencyScanIssueData.cs
    ├── Scanners/
    │   ├── AssetScanner.cs
    │   ├── IDependencyScanner.cs
    │   ├── ScannerOrchestrator.cs
    │   └── SerializedPropertyScanner.cs
    ├── Settings/
    │   ├── AnalyzerSettings.cs
    │   └── AnalyzerSettingsProvider.cs
    ├── UI/
    │   ├── GraphView/
    │   │   ├── CustomEdgeView.cs
    │   │   ├── CustomNodeView.cs
    │   │   └── DependencyGraphView.cs
    │   └── Styles/
    │       ├── EdgeStyle.uss
    │       ├── GraphWindow.uxml
    │       └── NodeStyle.uss
    └── Utils/
        ├── IconUtility.cs
        └── UnityTempDirectoryGuard.cs
```

### File Roles

- `DependencyAnalyzer.Editor.asmdef`
  - Editor 専用 Assembly Definition です。
  - Runtime build からツールコードを切り離します。

- `DependencyWindow.cs`
  - `EditorWindow` の entry point です。
  - UXML / USS の読み込み、GraphView の生成、Controller の初期化を担当します。

- `Controller/DependencyGraphController.cs`
  - Scan / Cancel / Zoom Step / Editor 選択同期など、ウィンドウ全体の操作を管理します。
  - Scanner 実行後に `DependencyGraphView` へ Model を渡します。

- `Controller/EditorSelectionSync.cs`
  - Graph node 選択時に Unity Editor の `Selection` と `PingObject` を同期します。

- `Core/DependencyNodeData.cs`
  - 1つの node の ID、path、display name、type、icon、file size、label、参照数などを保持します。

- `Core/DependencyEdgeData.cs`
  - node 間の依存方向、参照種別、Inspector property path、Missing 参照かどうかを保持します。

- `Core/DependencyGraphData.cs`
  - node / edge / scan issue の集合体です。
  - 参照数と被参照数の再計算も担当します。

- `Core/DependencyCache.cs`
  - スキャン済み node を再利用し、同じ対象の重複生成を抑えます。

- `Core/DependencyScanIssueData.cs`
  - スキャン中に検出した warning / error / info を保持します。

- `Scanners/IDependencyScanner.cs`
  - Scanner 追加のための共通 interface です。

- `Scanners/AssetScanner.cs`
  - Asset path、file size、labels、icon、Prefab / Mesh などの asset node 情報と Missing reference node を作成します。

- `Scanners/SerializedPropertyScanner.cs`
  - ロード済み Scene の Hierarchy、Component、SerializedProperty、Prefab source、Missing reference を解析します。
  - 表示対象 Component の参照と、Renderer の Material 参照を Inspector 参照として扱います。

- `Scanners/ScannerOrchestrator.cs`
  - 登録された scanner を順番に実行し、結果を1つの graph に統合します。
  - progress と cancellation の入口です。

- `Settings/AnalyzerSettings.cs`
  - 除外フォルダ、除外拡張子、scan yield batch size、zoom step などの設定を保持します。

- `Settings/AnalyzerSettingsProvider.cs`
  - Unity の Project Settings に Dependency Analyzer 設定 UI を登録します。
  - 共有設定 asset が存在しない場合は、作成ボタンを表示します。

- `UI/GraphView/DependencyGraphView.cs`
  - graph 全体の描画、layout、pan、zoom、minimap、node 展開、edge click、highlight animation を担当します。

- `UI/GraphView/CustomNodeView.cs`
  - node の icon、name、type、badge、`+/-`、ミートボールメニュー、parent jump、drag 操作を担当します。

- `UI/GraphView/CustomEdgeView.cs`
  - edge の曲線描画、実線 / 点線表現、edge click を担当します。

- `UI/Styles/GraphWindow.uxml`
  - toolbar と graph container の基本 layout を定義します。

- `UI/Styles/NodeStyle.uss`
  - node、badge、highlight、minimap、toolbar などの見た目を定義します。

- `UI/Styles/EdgeStyle.uss`
  - edge 種別ごとの style hook を定義します。

- `Utils/IconUtility.cs`
  - Unity 標準 icon の取得と node type class の判定をまとめます。

- `Utils/UnityTempDirectoryGuard.cs`
  - Unity の Temp folder が存在しない場合に補助的に作成し、AssetDatabase 周辺の警告を抑えます。

## Architecture

The tool follows an MVC-like structure.

```text
DependencyAnalyzer/
└── Editor/
    ├── Controller/
    ├── Core/
    ├── Scanners/
    ├── Settings/
    ├── UI/
    │   ├── GraphView/
    │   └── Styles/
    └── Utils/
```

- `Controller`
  - UI 操作、スキャン実行、Model 更新、Editor 選択同期を管理します。

- `Core`
  - `DependencyNodeData`、`DependencyEdgeData`、`DependencyGraphData`、`DependencyCache` など、UI に依存しないデータ構造を保持します。

- `Scanners`
  - Hierarchy、Component、SerializedProperty、Prefab 参照などを解析します。
  - `IDependencyScanner` により、将来的なスキャナー追加を想定しています。

- `UI/GraphView`
  - ノード、edge、minimap、pan / zoom、展開・収納アニメーションを描画します。

- `Settings`
  - 除外フォルダ、除外拡張子、スキャン単位、ズームステップなどの設定を扱います。

- `Utils`
  - Unity 標準アイコン取得や Temp ディレクトリ補助など、Editor API 依存処理を分離します。

## Dependency Types

- `Hierarchy`
  - GameObject の親子関係です。
  - 実線 edge で表示します。

- `Component`
  - GameObject に付与されている表示対象 Component です。
  - 実線 edge で表示します。

- `PrefabInstance`
  - Scene 上の Prefab instance から Prefab asset への関係です。
  - 実線 edge で表示します。

- `SerializedProperty`
  - Inspector 上の object reference です。
  - 点線 edge で表示し、初期状態ではミートボールメニュー配下に収納します。

- `MissingReference`
  - Missing Component または Missing Object Reference です。
  - Warning badge と tooltip で通知します。

## Design Notes

- GraphView API は使っていません。
  - Shader Graph のような connect / disconnect UI が不要なためです。
  - 依存関係の閲覧に特化した独自 UI にしています。

- Inspector 参照は通常展開と分離しています。
  - 通常の親子関係と Inspector 参照を混ぜるとグラフが急激に複雑になるためです。
  - 必要な時だけミートボールメニューから表示します。

- Asset 全体スキャンではなく、ロード済み Scene 中心です。
  - 実際にロード済み Scene の Hierarchy / Inspector から見えている依存関係を優先するためです。

- Node duplication is allowed.
  - グラフを DAG として厳密に共有すると edge が交差しやすくなるため、木構造として見やすくする目的で同じ対象を複数表示します。

## Current Limitations

- Unity Editor 専用です。Runtime build には含めません。
- 対象は現在ロード済み Scene から到達できる GameObject、Component、Asset 参照です。未ロード Scene や未使用 Asset 全体の棚卸しは対象外です。
- Unity が保持していない「あとから手で追加した標準 Component」と「生成時から付いていた標準 Component」の履歴は判別できません。
- そのため、冗長な標準 Component の除外はあらかじめ定義したルールに基づいています。
- Packages 配下など、Assets 配下の Script を持たない MonoBehaviour は Component ノードとして表示しません。
- Addressables や Resources.Load の文字列解析は現在の対象外です。

## License

Add license information here.
