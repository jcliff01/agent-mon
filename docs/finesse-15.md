# Cisco Finesse 15.0 compatibility target

Agent Mon targets **Cisco Finesse Release 15.0(1)**, with UCCE/PCCE and UCCX deployments considered during integration testing.

## Confirmed Cisco integration requirements

- Cisco Finesse gadgets are **OpenSocial XML** definitions embedded in the desktop layout.
- The Cisco Finesse JavaScript library is served by the Finesse server at `/desktop/assets/js/finesse.min.js`; jQuery is also required when using that library.
- Third-party gadgets may be hosted on the Finesse server or externally. Backend REST access must follow supported proxy/CORS and authentication requirements.
- Follow Cisco's Release 15 CSP guidance: avoid inline JavaScript, inline styles, inline handlers, eval and dynamic code execution.
- Use the Cisco release-matched sample gadgets and validate against the target Finesse 15 server.

## Agent Mon integration sequence

1. Replace the static HTML prototype with a release-compatible OpenSocial XML gadget, external CSS and JavaScript.
2. Read the signed-in Finesse user identity through supported APIs; map it securely to a registered collector identity.
3. Subscribe to relevant Finesse user/session notifications and record event timestamps.
4. Correlate notifications with collector HTTPS reachability measurements. Do not claim ICMP loss or RTP quality from HTTPS timing.
5. Use authenticated, least-privilege backend access for gadget and supervisor views. Never embed the collector ingestion API key in gadget source.
6. Test reconnect, Finesse failover, VPN interruption, DNS failure, endpoint shutdown and backend outage in a lab.

## References

- https://developer.cisco.com/docs/finesse/
- https://developer.cisco.com/docs/finesse/javascript-library-and-sample-gadgets/
- https://www.cisco.com/c/en/us/td/docs/voice_ip_comm/cust_contact/contact_center/finesse/finesse_1501/admin/guide/cfin_b_150_cisco-finesse-administration-guide/cfin_m_150_manage-desktop-layout.html

## Status

Version target and integration design documented. The current `gadget/index.html` is a standalone prototype, **not** a validated Finesse 15 gadget package. No live Cisco integration has been tested.
