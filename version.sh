#!/bin/bash

# Get the new version number
echo "New version (X.Y.Z): "
read VERSION

# Check if the version number is in the correct format (X.X.X)
if [[ ! $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: Version number must be in the format X.Y.Z"
  exit 1
fi

# Update the version number in Resources.resx
sed -i '' -e "s#<value>v[0-9]*\.[0-9]*\.[0-9]*</value>#<value>v$VERSION</value>#g" ./OpenVR2WS/Properties/Resources.resx

# Update the version number in package.json
sed -i '' -e "s/\"version\": \"[0-9]*\.[0-9]*\.[0-9]*\"/\"version\": \"${VERSION:1}\"/g" ./OpenVR2WS/Types/dist/package.json

# Add the changes to git
git add Resources.resx package.json

# Commit the changes with a message
git commit -m "Update version to $VERSION"

# Create a git tag with the version number
git tag $VERSION

# Push the changes and the tag to the remote repository
git push origin main
git push origin $VERSION

echo "Version updated to $VERSION and tag created."