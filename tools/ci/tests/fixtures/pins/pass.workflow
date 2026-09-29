# Every allowed form.
jobs:
  build:
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
      - name: Nested path
        uses: github/codeql-action/init@2892aa5e19bbd11bc0cff5427e3b750a04d9e3c2 # v4.38.2
      - uses: "actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68" # v6.0.0
      - uses: ./.github/actions/local
      - uses: docker://alpine@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
  reuse:
    uses: owner/repo/.github/workflows/shared.yml@3d3c42e5aac5ba805825da76410c181273ba90b1 # v1.2.3
