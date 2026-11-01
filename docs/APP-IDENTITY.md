# Skoven — App Identity

All platform identity, signing, and publishing information for the Meta Horizon Store.

---

## Meta Developer Dashboard

| Field | Value |
|---|---|
| App Name | Skoven |
| App ID | **1122808136998612** |
| Dashboard URL | https://developer.oculus.com/manage/applications/1122808136998612 |
| Owner | Sirrine-Jonathan |
| Platform | Meta Quest (Android/OpenXR) |
| Package name | `com.sirrine.skoven` |
| Target devices | Quest 3 (primary), Quest 2 (secondary) |
| Created | 2026-10-31 |

---

## Where the App ID is used

| Location | Purpose |
|---|---|
| `unity/Assets/Resources/OculusSettings.asset` | Read by Meta XR SDK at runtime to identify the app to the platform |
| `unity/ProjectSettings/ProjectSettings.asset` | Android package name matches dashboard entry |
| `unity/Assets/Skoven/Scripts/Settings/OculusAppConfig.cs` | Typed constant for any runtime platform checks |

**If the App ID ever changes** (it shouldn't), update all three locations above and this document.

---

## Build and signing requirements

- **Development builds** (sideloading via ADB): no signing required beyond a debug keystore. Use `adb install -r Skoven.apk`.
- **App Lab / Store submission**: requires a release keystore. Generate once, store securely outside the repository, never commit.
  - Keystore location: outside `skoven/` — do not put it in the repo.
  - Record the keystore alias here once created (not the password or file).
- **App ID must be set** in `OculusSettings.asset` before any build that will run on a device paired to this developer account. Builds without a valid App ID will not be able to access platform services (leaderboards, IAP, entitlement checks).

---

## Entitlement check (Stage 5+)

For release builds, implement an entitlement check at app launch using the Platform SDK:

```csharp
// In a MonoBehaviour Start() or Awake():
Oculus.Platform.Core.Initialize(OculusAppConfig.AppId);
Oculus.Platform.Entitlements.IsUserEntitledToApplication().OnComplete(EntitlementCallback);
```

For the Stage 4 prototype, entitlement checking is not required — development builds sideloaded to a device registered to the developer account bypass it automatically.

---

## Channel plan

| Channel | Purpose | When |
|---|---|---|
| Development | Local sideload via ADB | Now — Stage 4 prototype |
| App Lab (Alpha) | Internal testers and selected external testers | Stage 6+ |
| App Lab (Beta) | Wider test group | Stage 7 |
| Production | Public Meta Horizon Store | Stage 9 |

Do not submit to Production until Stage 8 release-candidate readiness is complete.

---

## Notes

- The Meta developer app entry was created manually via the dashboard. No automated tooling has touched it.
- App Lab listing, Store assets, pricing, and content ratings have not been configured yet. These are Stage 8 tasks.
- Privacy policy URL is required before any public distribution. Draft in Stage 7.
