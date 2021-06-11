AddTextChankToPngInZip
======================

[![Build status](https://ci.appveyor.com/api/projects/status/bo5fh2d8sew2fcs2?svg=true)](https://ci.appveyor.com/project/koturn/addtextchanktopnginzip "AppVeyor | koturn/AddTextChankToPngInZip")

## 概要

clusterの写真ファイル名を日付形式にリネームし，tEXtチャンクに撮影日時を入れるやつ


## 説明

タイトル通りです．

clusterの画像ファイル名はGUID形式なので，それをファイル更新日付（作成日付）を含むファイル名にして整理しやすくします．

対象とするのは画像を複数選択してダウンロードするときに得られるzipファイルです．
zipファイル内の全PNGファイルに対し，下記2つの処理を行い，新たにzipファイルを作成し格納します．

1. 作成日付を捨い，`cluster_yyyyMMdd_HHmmss_nnn.png` というファイル名にする
    - `yyyy` は年
    - `MM` は月（0埋めあり）
    - `dd` は日（0埋めあり）
    - `HH` は時（0埋めあり）
    - `mm` は分（0埋めあり）
    - `ss` は秒（0埋めあり）
    - `nnn` は3桁の連番（0埋めあり，同一時刻に関して採番）
2. 以下の2つのtEXtチャンクを追加する
    - Key: `Title`, Value: 元のファイル名
    - Key: `Creation Time`, Value: `yyyy:MM:dd HH:mm:ss.fff` 形式の日付（`explroer.exe` のプロパティで表示可能な形式）
3. tIMEチャンクを追加する

元のzipファイルは例えば，`photos.zip` という名前なら，`photos.old.zip` という名前にリネームし，バックアップとして残します．


## 注意とか

clusterで撮影した写真について，2021/6/11現在，ユーザが撮影時刻を知る手段は複数選択のzipファイル内のファイル更新日時を見るしかありません．
単一でPNGファイルを保存した場合，ファイル作成日時，更新時刻は保存日時になるため，そもそも撮影日時を知ることはできません．

もし，zipファイルを既に展開しており，更新時刻を変更した後，再度zip圧縮してこのツールを使用しても，変更された更新時刻になります．

また，zipファイルの仕様上，保持できる更新時刻の精度は2秒刻みです（拡張フィールドを用いない限り）．
clusterのzipファイル内のPNGファイルの更新時刻のミリ秒以下は失われております．

そのため，2秒以内に撮影した複数の写真が同一時刻となりますが，ファイル名としては同一時刻に対する連番を付与することで回避しています．
大半の写真ファイルの連番部分が `000` となると思います．


## 使用方法

```shell
> AddTextChankToPngInZip.exe [clusterのWebページからダウンロードした画像入ったzipファイル]
```

exeファイルにzipファイルをD&Dしても大丈夫です．


## TODO

- GUIを付ける
- ファイル名フォーマットをハードコーディングしないようにする


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
