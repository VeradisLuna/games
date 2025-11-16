#!/usr/bin/env bash
set -euo pipefail

OUT_DIR="$1"
PUZZLE_DIR="$2"

OUT="$OUT_DIR/rss.xml"
BASE_URL="https://lunamini.io/mini"
CHANNEL_TITLE="LunaMini.io"
CHANNEL_LINK="https://lunamini.io/mini"
CHANNEL_DESC="Daily mini crossword from LunaMini.io"

today=$(date -u +"%Y-%m-%d")

mkdir -p "$OUT_DIR"

cat > "$OUT" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>${CHANNEL_TITLE}</title>
    <link>${CHANNEL_LINK}</link>
    <description>${CHANNEL_DESC}</description>
    <language>en-us</language>
EOF

mapfile -t FILES < <(
  for f in "$PUZZLE_DIR"/*.json; do
    jq -r '.date + " " + $f' --arg f "$f" "$f"
  done | sort -r | head -n 30 | awk '{print $2}'
)

for f in "${FILES[@]}"; do
  date=$(jq -r '.date' "$f")

  # Skip future puzzles
  if [[ "$date" > "$today" ]]; then
    continue
  fi

  title=$(jq -r '.title' "$f")
  author=$(jq -r '.author' "$f")

  pubdate=$(date -u -d "$date" +"%a, %d %b %Y 00:00:00 +0000")
  link="${BASE_URL}/${date}"

  cat >> "$OUT" <<EOF
    <item>
      <title>${title} (Mini crossword)</title>
      <link>${link}</link>
      <guid>${link}</guid>
      <pubDate>${pubdate}</pubDate>
      <description>${title} — a LunaMini crossword by ${author} for ${date}.</description>
      <author>${author}</author>
    </item>
EOF
done

cat >> "$OUT" <<EOF
  </channel>
</rss>
EOF