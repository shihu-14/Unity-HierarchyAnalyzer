# Unity Dependency Analyzer

Unity 6 向けのエディタ拡張です。現在開いているシーンの Hierarchy に存在する GameObject、ユーザーが追加した Component、Inspector 上の参照、Prefab 由来の参照などをグラフとして可視化し、依存関係の把握、Missing Reference の調査、不要な参照の発見を支援します。

このツールは Asset Store 配布品質を意識し、Model / View / Controller を分離した構成で実装しています。解析処理、データ保持、UI 描画を分けることで、Unity の UI Toolkit 変更や今後の解析ルール追加に対する影響範囲を小さくしています。

## Demo / Screenshots

> Add screenshots or GIFs here.

<!--
Example:

![Dependency graph demo](docs/images/dependency-graph-demo.png)
-->

## Features

- Open scene dependency graph
  - 現在開いているシーンの Hierarchy に存在する Object を対象に可視化します。
  - 未使用アセット全体のスキャン結果はグラフに出さず、シーンで使われているものを中心に表示します。

- Scene object and component analysis
  - GameObject 間の親子関係を抽出します。
  - ユーザーが追加した Component をノードとして表示します。
  - Transform、標準生成時に付く Camera / Light など、冗長になりやすい標準 Component は原則として省略します。

- Inspector reference analysis
  - SerializedProperty を使い、Inspector で参照されている Object、Component、Asset を抽出します。
  - Inspector 参照はデフォルトでは展開せず、ノード上のミートボールメニューから表示します。
  - AudioSource の AudioClip、Renderer の Material なども Inspector 参照として扱います。

- Graph visualization
  - Hierarchy などの通常依存は実線 edge で表示します。
  - Inspector 参照は点線 edge で表示します。
  - edge は折れ線ではなく曲線で描画し、視認性を高めています。
  - 同じノードが複数箇所に現れる場合でも木構造として表示し、root に最も近いもの以外は初期状態で収納します。

- Node design
  - Object、Prefab、C# Script、Audio、Material、Camera、Canvas などをアイコンと色で区別します。
  - ノードは深い階層ほど小さく表示します。
  - フォントサイズは一定にし、小さいノードでも読みやすさを保ちます。
  - ノード本文にはパスを表示せず、ホバー時の tooltip に詳細情報を表示します。

- Lazy expansion
  - 初期表示では深さを制限し、必要な場所だけを `+` ボタンで展開します。
  - Inspector 参照は `+` では開かず、ミートボールメニューでのみ展開します。
  - `-` ボタンで対象ノード以下を収納できます。

- Editor synchronization
  - グラフ上のノードをクリックすると Unity の Hierarchy / Project で対象を Ping し、Inspector に表示します。
  - Unity 側で Object を選択した場合、対応するグラフノードへ視点を移動します。
  - edge や親方向ボタンからノードへジャンプした際は、短い黄色い縁のフラッシュで対象を示します。

- Navigation support
  - ノードのドラッグ移動に対応しています。
  - ズームステップは toolbar のスライダーで調整できます。
  - 右上の minimap からグラフ全体の位置を把握し、大まかに移動できます。

- Missing reference support
  - Missing Component や Missing Object Reference を検出します。
  - 非表示の子に Missing Reference がある場合は、最も近い可視親ノードへ警告を伝播します。

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

4. ウィンドウが開くと、現在開いているシーンを自動でスキャンします。必要に応じて `Scan` ボタンで再スキャンできます。

## Usage

- `Scan`
  - 現在開いているシーンの依存関係を再解析します。

- `Cancel`
  - 実行中のスキャンをキャンセルします。

- `Zoom Step`
  - トラックパッドやマウスホイールのズーム感度を調整します。

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

## Design Notes

- GraphView API は使っていません。
  - Shader Graph のような connect / disconnect UI が不要なためです。
  - 依存関係の閲覧に特化した独自 UI にしています。

- Inspector 参照は通常展開と分離しています。
  - 通常の親子関係と Inspector 参照を混ぜるとグラフが急激に複雑になるためです。
  - 必要な時だけミートボールメニューから表示します。

- Asset 全体スキャンではなく、開いているシーン中心です。
  - 実際に Hierarchy / Inspector から見えている依存関係を優先するためです。

- Node duplication is allowed.
  - グラフを DAG として厳密に共有すると edge が交差しやすくなるため、木構造として見やすくする目的で同じ対象を複数表示します。

## Current Limitations

- Unity Editor 専用です。Runtime build には含めません。
- Unity が保持していない「あとから手で追加した標準 Component」と「生成時から付いていた標準 Component」の履歴は判別できません。
- そのため、冗長な標準 Component の除外はあらかじめ定義したルールに基づいています。
- Addressables や Resources.Load の文字列解析は現在の対象外です。

## License

Add license information here.
