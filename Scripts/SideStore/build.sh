#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p build/SideStore
xcodebuild -version | tee build/SideStore/xcode-version.txt
xcrun --sdk iphoneos --show-sdk-version | tee build/SideStore/sdk-version.txt
python3 Scripts/SideStore/prepare.py
plutil -lint OwlLuna.xcodeproj/project.pbxproj
xcodebuild archive \
  -project OwlLuna.xcodeproj -scheme OwlLuna -configuration Release \
  -destination 'generic/platform=iOS' \
  -archivePath "$PWD/build/OwlLuna.xcarchive" \
  -derivedDataPath "$PWD/build/DerivedData" \
  -xcconfig Config/SideStore.xcconfig \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY= \
  CODE_SIGN_ENTITLEMENTS= DEVELOPMENT_TEAM= \
  2>&1 | tee build/SideStore/archive.log
app="$PWD/build/OwlLuna.xcarchive/Products/Applications/OwlLuna.app"
test -d "$app"
mkdir -p build/SideStore/staging/Payload
ditto "$app" build/SideStore/staging/Payload/OwlLuna.app
# Linker ad-hoc signatures carry no developer identity; remove before re-signing.
while IFS= read -r -d '' binary; do
  if file "$binary" | grep -q 'Mach-O'; then
    if codesign -d "$binary" >/dev/null 2>&1; then
      codesign --remove-signature "$binary"
    fi
  fi
done < <(find build/SideStore/staging/Payload -type f -print0)
find build/SideStore/staging/Payload -name _CodeSignature -type d -exec rm -rf {} +
find build/SideStore/staging/Payload -name embedded.mobileprovision -type f -delete
ditto -c -k --keepParent build/SideStore/staging/Payload build/SideStore/OwlLuna-unsigned.ipa
python3 Scripts/SideStore/verify.py build/SideStore/OwlLuna-unsigned.ipa --macos
