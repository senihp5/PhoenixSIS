# Draft email to TEA - third-party SIS vendor onboarding (TSDS Ed-Fi API)

**Status:** DRAFT for Patrick to review and send. Not sent by Claude.
**To:** TSDSCustomerSupport@tea.texas.gov; TSDSDeployment@tea.texas.gov
**Suggested subject:** Source-system vendor onboarding for a new Ed-Fi-based SIS (TSDS)

**Framing note (2026-11-11):** rewritten from the earlier "LEA in-house build" angle to a
third-party vendor inquiry. Rationale in `texas-tsds-requirements.md` section 4a - the
commercial source-system vendor path is the documented one; the in-house path was the
undocumented question. Patrick is both the independent developer and New Heights' IT
Director, so the draft discloses both hats and presents New Heights as a prospective
pilot LEA, not as the builder.

---

Hello,

I'm an independent developer building a new student information system on the Ed-Fi
ODS/API, designed for adult high school programs (working name Phoenix SIS). Before I
take it further, I'd like to understand how a new SIS vendor onboards to the TSDS Ed-Fi
API as a source system.

In the interest of transparency: I'm also the IT Director at New Heights, an
open-enrollment charter in Fort Worth that operates an adult high school program. New
Heights would be a prospective design partner and first pilot LEA for this system. Today
New Heights reports through its existing SIS; this is exploratory, and I'm writing in my
capacity as the system's developer.

My understanding is that, since the August 2024 move to API transactions, TSDS data is
loaded through the Ed-Fi API directly from source-system vendors, with Key/Secret
credentials issued per LEA ODS. I'd appreciate your help with the following:

1. What is the process for a new SIS vendor to become a registered source-system vendor
   for the TSDS Ed-Fi API?

2. Is there a vendor test/sandbox environment, and is Ed-Fi API certification required?
   Is vendor testing done through the Texas Education Exchange, or another channel?

3. How are production Key/Secret credentials issued for a specific LEA's ODS, and what is
   the LEA's role in authorizing a vendor's system to submit on its behalf?

4. What onboarding, testing, or parallel-submission requirements apply before a new
   source system's first live PEIMS submission - for example the Level 1-3 business
   validations and the Vendor Data Completeness Scorecard?

5. Where is new-vendor API onboarding documented? I would like the authoritative
   reference for the current TEDS API specification and credentialing steps (I was not
   able to reach the TWEDS API overview online).

Any pointers to the right contacts or documentation would be very helpful.

Thank you,

Patrick Hines
Developer, Phoenix SIS (working name)
IT Director, New Heights
[phone / preferred contact]
