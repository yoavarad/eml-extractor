#!/usr/bin/env bash
# Fetch the public-mail harvest corpus into a gitignored directory (README "Real-world tests", docs/edge-cases.md).
# Usage: bash scripts/fetch-public-corpus.sh [dest]   (default: corpus/internal; a relative dest is relative to the repo root)
# Downloaded files are untrusted data: they are only downloaded, hash-checked and unpacked. Nothing from them is executed.
# Idempotent: finished sources (marker dest/<source>/.fetched) and existing files are skipped.
# Exit: 0 all sources present, 2 some source skipped or failed (see the summary at the end).
set -uo pipefail
cd "$(dirname "$0")/.."

dest="${1:-corpus/internal}"
mkdir -p "$dest"
dest="$(cd "$dest" && pwd)"

# Pinned inputs (fetched 2026-11-04).
SA_URL="https://spamassassin.apache.org/old/publiccorpus"
SA_FILES=(
  "20030228_easy_ham.tar.bz2 2b7b65904bcfcc31d2b5f51946f2d261370b257402cbbd62930b46ab83367438"
  "20030228_easy_ham_2.tar.bz2 b4bd3dc5ae5b40f38e99a0e41ad7d16b428b56e818e3a6ffa07330d62004dd38"
  "20030228_hard_ham.tar.bz2 ce2ce67880643dbde65ea7f85bffbfe4417349c4bd80b6b0de56262ae6b0a9c9"
  "20030228_spam.tar.bz2 c08debc32413804949a866be45ef78195cec2cbafd1da744ed76cdb860589743"
  "20030228_spam_2.tar.bz2 40bc444c596ff3e71505ae334f115b16f9b946786ee68f09b921b6b99217fd00"
  "20050311_spam_2.tar.bz2 44280a0e28bf7645b2279e8e42659271ad82c6f40bf8d364dbda7f1df344a765"
)
NZ_URL="https://monkey.org/~jose/phishing"
# remote-name local-name sha256 (year files have no extension upstream; the local name gets .mbox so the probe treats them as mbox)
NZ_FILES=(
  "20051114.mbox 20051114.mbox 928cbf1c394d01d0b398ded8dc7cbd94dd4ce9cb0d4efdecd12696338c247926"
  "phishing0.mbox phishing0.mbox 6184b0a34ed8cbbb252676e21c1eab9eac5fa4be89c61e30cc9954ebbb25d848"
  "phishing1.mbox phishing1.mbox 99f02474a11086408a5ce6d11ef38823f003c6611a4312c31fbee96773eeba28"
  "phishing2.mbox phishing2.mbox 5113277984eae759a5ff958ccf408b542441ce033a49b923ab3b0f324075e6bd"
  "phishing3.mbox phishing3.mbox b29336d2e31c2dff19639415e98ed2aced5b4d63675ea5e29bea1a7d4e452841"
  "private-phishing4.mbox private-phishing4.mbox 2f246d27b9838a4eff1df46cd271d2a968453b1352e97dfd239a5ce135dd88ce"
  "phishing-2015 phishing-2015.mbox 7523e62a5e6e4ee8134ab8b1a628e423097a7779e1003b28422864aa34f1c319"
  "phishing-2016 phishing-2016.mbox 42393e4d686193bfb879d1608c23fd4d3b698ccd4d73f0e1d2276ba709718483"
  "phishing-2017 phishing-2017.mbox 110fba876b38b44035bf065788952674bc68a7b6879c345a515a9ac844b1d704"
  "phishing-2018 phishing-2018.mbox 83f49cf2a76c7b29b205d22d8778e46848e36af0f15ee1df126a543696acd48d"
  "phishing-2019 phishing-2019.mbox cab39d6fcb38cf80cdaabb3e6ffd1222777e2fcaac7e8480321b7f729c2e3c0e"
  "phishing-2020 phishing-2020.mbox 99e20bf25f5e8556d37103f2c8140cd75181a62d3df91df8acf9d9aece9f08bb"
  "phishing-2021 phishing-2021.mbox 636707cff42245d1482e4ae8a2c03369717a5e06bf9d1a0f4ec5dcd846661cee"
  "phishing-2022 phishing-2022.mbox a6291e2cb990204766fce425423949b403cce4ae6a660fc8d08812e40a063fa7"
  "phishing-2023 phishing-2023.mbox 5007f1c1dc91b15e93e988afa3a1cf402e15c88f765d3057be8bfec50dae1fe8"
  "phishing-2024 phishing-2024.mbox 60afa40a2757170093a2bde31ec9cda4e54d33992203ce04bfbbc86d2e5e5b99"
  "phishing-2025 phishing-2025.mbox f1fa7e0fe35c9a16f36d9aa20ade7e1d3908d1d0c1d917b8c53c8aa799ec1c8f"
)
NZ_LICENSE_SHA="9ba9550ad48438d0836ddab3da480b3b69ffa0aac7b7878b5a0039e7ab429411"
CW_REPO="https://github.com/cw-l/email-corpus";                         CW_SHA="45ebc57438fb2ee908aa53cc7192ad82bcc23603"
MK_REPO="https://github.com/jstedfast/MimeKit";                         MK_SHA="03097f0474d554284c1fb73692e95aecc1003bec"
PM_REPO="https://github.com/php-mime-mail-parser/php-mime-mail-parser"; PM_SHA="4fbd8ccc3c73e87689f9fbd487b92573279ea107"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
skipped=()

sha256_of() { if command -v sha256sum >/dev/null; then sha256sum "$1" | cut -d' ' -f1; else shasum -a 256 "$1" | cut -d' ' -f1; fi; }

# download <url> <out> <sha256>; non-zero on any failure (including an unreadable file, e.g. removed by antivirus)
download() {
  curl -fsSL --retry 3 -o "$2" "$1" || return 1
  [ -r "$2" ] || return 1
  [ "$(sha256_of "$2" 2>/dev/null)" = "$3" ] || { echo "  verify failed: $1" >&2; return 1; }
}

# Shallow, blob-filtered, sparse checkout of one pinned commit into <outdir> (no autocrlf, no hooks).
git_fetch_tree() { # repo sha outdir sparse-path
  local repo="$1" sha="$2" out="$3" path="$4"
  mkdir -p "$out" &&
  git -C "$out" init -q &&
  git -C "$out" config core.autocrlf false &&
  git -C "$out" config core.hooksPath /dev/null &&
  git -C "$out" remote add origin "$repo" &&
  git -C "$out" fetch -q --depth 1 --filter=blob:none origin "$sha" &&
  git -C "$out" sparse-checkout set "$path" &&
  git -C "$out" checkout -q FETCH_HEAD
}

done_marker() { [ -e "$dest/$1/.fetched" ]; }
mark() { mkdir -p "$dest/$1" && date -u +%Y-%m-%d > "$dest/$1/.fetched"; }

echo "dest: $dest"

# --- SpamAssassin public corpus (2003 + 2005) ---
if done_marker spamassassin; then
  echo "spamassassin: present, skip"
else
  ok=1; stage="$tmp/sa-stage"; mkdir -p "$stage" "$tmp/sa-dl"
  for e in "${SA_FILES[@]}"; do
    read -r f h <<<"$e"
    echo "spamassassin: $f"
    if download "$SA_URL/$f" "$tmp/sa-dl/$f" "$h"; then
      tar -xjf "$tmp/sa-dl/$f" -C "$stage" --no-same-owner || ok=0
    else
      ok=0
    fi
  done
  if [ "$ok" = 1 ]; then
    mkdir -p "$dest/spamassassin" && cp -R "$stage"/. "$dest/spamassassin"/ && mark spamassassin
  else
    skipped+=("spamassassin")
  fi
fi

# --- Jose Nazario phishing mboxes (CC-BY-4.0, https://monkey.org/~jose/phishing/) ---
mkdir -p "$dest/nazario"
if [ ! -e "$dest/nazario/LICENSE" ] && download "$NZ_URL/LICENSE.txt" "$tmp/nz-license" "$NZ_LICENSE_SHA"; then
  cp "$tmp/nz-license" "$dest/nazario/LICENSE" || skipped+=("nazario/LICENSE")
fi
for e in "${NZ_FILES[@]}"; do
  read -r remote local h <<<"$e"
  if [ -e "$dest/nazario/$local" ]; then echo "nazario: $local present, skip"; continue; fi
  echo "nazario: $remote"
  if download "$NZ_URL/$remote" "$tmp/nz-$local" "$h"; then
    cp "$tmp/nz-$local" "$dest/nazario/$local.part" && mv "$dest/nazario/$local.part" "$dest/nazario/$local"
  else
    skipped+=("nazario/$local (download/verify failed; antivirus may have removed it)")
  fi
  rm -f "$tmp/nz-$local"
done

# --- git sources ---
fetch_git_source() { # name repo sha sparse-path dest-subdir
  local name="$1" repo="$2" sha="$3" path="$4" sub="$5"
  if done_marker "$sub"; then echo "$name: present, skip"; return; fi
  echo "$name: $repo @ ${sha:0:12}"
  local w="$tmp/git-$name"
  if git_fetch_tree "$repo" "$sha" "$w" "$path" && [ -d "$w/$path" ]; then
    mkdir -p "$dest/$sub" && cp -R "$w/$path"/. "$dest/$sub"/ && mark "$sub"
  else
    skipped+=("$name")
  fi
}
fetch_git_source cw-l-email-corpus "$CW_REPO" "$CW_SHA" "submissions" "cw-l-email-corpus"
fetch_git_source mimekit-testdata "$MK_REPO" "$MK_SHA" "UnitTests/TestData" "mimekit-testdata"
# php-mime-mail-parser fixtures (issue149, issue158a, issue274.eml, m0129, ...) at the path InternalCorpusTests expects
fetch_git_source php-mails "$PM_REPO" "$PM_SHA" "tests/mails" "spec/php-mails"

{
  echo "Fetched $(date -u +%Y-%m-%d) by scripts/fetch-public-corpus.sh. Public mail; licenses and PII vary. Do not commit."
  echo "spamassassin/       $SA_URL/ (20030228_* and 20050311_spam_2)"
  echo "nazario/            $NZ_URL/ (CC-BY-4.0, Jose Nazario)"
  echo "cw-l-email-corpus/  $CW_REPO @ $CW_SHA (submissions/)"
  echo "mimekit-testdata/   $MK_REPO @ $MK_SHA (UnitTests/TestData)"
  echo "spec/php-mails/     $PM_REPO @ $PM_SHA (tests/mails)"
} > "$dest/SOURCES.txt"

echo
if [ "${#skipped[@]}" -gt 0 ]; then
  echo "SKIPPED/FAILED:"
  printf '  %s\n' "${skipped[@]}"
  exit 2
fi
echo "all sources present"
