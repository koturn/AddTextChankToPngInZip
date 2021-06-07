AddTextChankToPngInZip
======================

## 概要

clusterの写真ファイル名を日付形式にリネームし，tEXtチャンクに撮影日時を入れるやつ

## 説明

タイトル通りです．

clusterの画像ファイル名はGUID形式なので，それをファイル更新日付（作成日付）を含むファイル名にして整理しやすくします．

対象とするのは画像を複数選択してダウンロードするときに得られるzipファイルです．
zipファイル内の全PNGファイルに対し，下記2つの処理を行い，新たにzipファイルを作成し格納します．

1. 作成日付を捨い，`cluster_yyyyMMdd_HHmmss.fff.png` というファイル名にする
2. 以下の2つのtEXtチャンクを追加する
    - Key: `Title`, Value: 元のファイル名
    - Key: `Creation Time`, Value: `yyyy:MM:dd HH:mm:ss.fff` 形式の日付（`explroer.exe` のプロパティで表示可能な形式）

元のzipファイルは例えば，`photos.zip` という名前なら，`photos.old.zip` という名前にリネームします．

## 使用方法

```shell
> AddTextChankToPngInZip.exe [clusterのWebページからダウンロードした画像入ったzipファイル]
```

exeファイルにzipファイルをD&Dしても大丈夫です．


## TODO

- WPFのものではなく，いい感じのPNGライブラリを使うこと


## LICENSE

This software is released under the MIT License, see [LICENSE](LICENSE "LICENSE").
