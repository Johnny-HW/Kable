<!-- Kable Implementation Layout Document -->
<div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b; line-height: 1.6; max-width: 100%; margin: 0 auto;">

<!-- Hero Header Banner -->
<div style="background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0369a1 100%); border-radius: 14px; padding: 32px 28px; margin-bottom: 28px; color: #ffffff; box-shadow: 0 10px 25px -5px rgba(15, 23, 42, 0.25);">
<div style="display: inline-flex; align-items: center; gap: 6px; background: rgba(56, 189, 248, 0.18); border: 1px solid rgba(56, 189, 248, 0.4); padding: 4px 12px; border-radius: 20px; font-size: 11.5px; font-weight: 700; letter-spacing: 0.5px; color: #38bdf8; margin-bottom: 14px;">
<span>📦 PACKAGING & TAXONOMY</span>
</div>
<h1 style="margin: 0 0 10px 0; font-size: 28px; font-weight: 800; letter-spacing: -0.5px; color: #ffffff; border-bottom: none; padding-bottom: 0;">
      04. Implementation & Directory Layout
</h1>
<p style="margin: 0; font-size: 14.5px; color: #94a3b8; max-width: 780px; line-height: 1.6;">
      Repository directory taxonomy, namespace decomposition, and standalone NuGet distribution layout for <code>Kable</code> and <code>Kable.Core</code>.
</p>
<div style="margin-top: 18px; display: flex; flex-wrap: wrap; gap: 8px;">
<span style="background: #0284c7; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Kable.nupkg</span>
<span style="background: #059669; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Kable.Core.nupkg</span>
<span style="background: #6366f1; color: #ffffff; font-size: 11px; font-weight: 600; padding: 3px 10px; border-radius: 12px;">Roslyn Source Generators</span>
</div>
</div>

<!-- Repository Tree -->
<div style="background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; margin-bottom: 24px; overflow: hidden;">
<div style="background: #f8fafc; border-bottom: 1px solid #e2e8f0; padding: 12px 18px; font-weight: 700; font-size: 14px; color: #1e293b;">
      📁 Standalone Repository Structure
</div>
<div style="padding: 14px 18px;">

```
Kable/                                         # [Repository Root]
│
├── .github/workflows/
│   ├── ci-cd.yml                              # Automated build, test, and NuGet package publish
│   └── pages.yml                              # GitHub Pages static documentation deployment
│
├── Kable.sln                                  # Standalone solution
├── README.md                                  # Library overview and fluent quick-start
├── AGENTS.md                                  # Workspace rules and governance
├── CONTRIBUTING.md                            # Contribution guidelines
├── CHANGELOG.md                               # Release history (SemVer)
│
├── docs/                                      # [GitHub Pages Documentation Root]
│   ├── index.html                             # Docsify static single-page application (i18n)
│   ├── _navbar.md                             # Global language switcher navbar
│   │
│   ├── en/                                    # [English Documentation Root]
│   │   ├── _sidebar.md                        # Categorized navigation sidebar
│   │   ├── _navbar.md                         # Language navbar
│   │   ├── README.md                          # Quick-start guide
│   │   └── *.md                               # 01~07 technical specifications
│   │
│   └── ko/                                    # [Korean Documentation Root]
│       ├── _sidebar.md                        # Korean navigation sidebar
│       ├── _navbar.md                         # Language navbar
│       ├── README.md                          # Quick-start guide (Korean)
│       └── *.md                               # 01~07 technical specifications (Korean)
│
├── src/
│   ├── Kable.Core/                            # [Pure Abstraction Contracts]
│   │   ├── IConnectionContext.cs              # Bedrock Pipelines input/output
│   │   ├── IProtocolCodec.cs                  # Zero-allocation framing contract
│   │   └── Checksums/                         # CRC-16, LRC, XOR BCC algorithms
│   │
│   └── Kable/                                 # [Engine Implementation & Adapters]
│       ├── Transports/                        # TCP, SerialPort, NamedPipe
│       ├── Engine/                            # KableSession (FIFO lock + lock-free router)
│       ├── Observability/                     # Tri-stream ringbuffer (ICommObserver)
│       └── Extensions/                        # KableClientBuilder & DI extensions
│
└── tests/
    └── Kable.Tests/                           # Unit, fault injection, and contract tests
```

</div>
</div>

</div>
