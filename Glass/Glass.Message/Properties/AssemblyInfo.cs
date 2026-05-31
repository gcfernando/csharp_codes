// -----------------------------------------------------------------------------
//  Glass.Message — hand-authored assembly metadata. GenerateAssemblyInfo is
//  turned off in the .csproj so this file is the single source of truth for the
//  assembly's identity, version, and COM visibility.
//
//  File        : AssemblyInfo.cs
//  Developer   ::> Gehan Fernando
// -----------------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Human-readable identity shown in the file's Details tab and by tooling.
[assembly: AssemblyTitle("Glass.Message")]
[assembly: AssemblyDescription("Next-level modern replacement for MessageBox — Mica, Acrylic, theming, animations, toast, async, input, countdown, RTL, and more.")]
[assembly: AssemblyCompany("Gehan Fernando")]
[assembly: AssemblyProduct("Glass.Message")]
[assembly: AssemblyCopyright("Copyright © 2026 Gehan Fernando")]
// The library has no COM surface; the GUID is only the type-library identity
// required when ComVisible metadata is emitted.
[assembly: ComVisible(false)]
[assembly: Guid("2a291aee-854d-4e3e-81e7-721b4d829213")]

// Versioning. Keep these in step with <Version> in Glass.Message.csproj.
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

// Lets the test project reach the library's internal types (GlassDialog,
// GlassDialogConfig, RoundRect, …).
[assembly: InternalsVisibleTo("Glass.Message.Tests")]
