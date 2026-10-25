# Pilot checklist (M6)

Pilot: 2–3 kitchens in Ace Divino for two weeks. The goal is real orders, and learning what the
full MVP (M7) should include.

## Before inviting anyone

- [ ] **SMS sign-in works in production** with a DLT-registered sender and OTP template. Without
      it nobody can sign in on production. (Needs the SMS provider integration; see below.)
- [ ] Production deployed, HTTPS works on `towerhaat.com`, `/api/health` says ok.
- [ ] Both scheduled tasks run (Admin → Error log stays quiet; an unaccepted test order expires
      after 30 minutes).
- [ ] Push keys set; "Turn on notifications" works on an Android phone.
- [ ] Ace Divino flat list uploaded.
- [ ] Terms and privacy notice reviewed by a lawyer; operator name and contact email filled in
      (`web/src/app/legal/legal.ts`).
- [ ] A backup downloaded and stored safely.

## Bug bash: three phones

Do every row on each phone: **Android (Chrome)**, **iPhone (Safari, then installed to the home
screen)**, and a **low-end Android** (2–3 GB RAM). Use staging, with two phones at once where a
row needs an owner and a customer.

| # | Check | Android | iPhone | Low-end |
| --- | --- | --- | --- | --- |
| 1 | Sign up: code arrives (or shows on staging), name, tower, flat, consent | | | |
| 2 | Install: Android "Install" card; iPhone Share → Add to Home Screen; opens full screen | | | |
| 3 | Register a business, add photos (from the camera too), wait for approval | | | |
| 4 | Menu: add items with photos, daily limit, switch one off for today | | | |
| 5 | Customer: home shows the business, open badge right, menu shows sold out and "only N left" | | | |
| 6 | Cart and checkout: as soon as possible, later today, tomorrow; COD and UPI | | | |
| 7 | Owner hears the new order: chime and banner with the app open, push with it closed | | | |
| 8 | Accept, preparing, out for delivery, delivered: the customer's screen follows live | | | |
| 9 | Customer gets a push for each step with the app closed (iPhone: installed app only) | | | |
| 10 | UPI: "Pay with UPI app" opens GPay, PhonePe and Paytm with the amount; QR scans from another phone; UPI ID copies | | | |
| 11 | "I've paid" → owner sees it → UPI received; also try "Not received" | | | |
| 12 | COD: cash collected after delivery | | | |
| 13 | Reject with a reason; customer cancel while waiting; owner cancel after accepting | | | |
| 14 | Leave an order unaccepted: reminder push at 10 minutes, expires at 30 | | | |
| 15 | Prep summary adds up | | | |
| 16 | Slow network (Chrome DevTools "Slow 3G", or a weak signal): home and business page usable | | | |
| 17 | Sign out and in again; delete a test account | | | |

Note anything odd with a screenshot and the time; the time helps find it in Admin → Error log.

**UPI note:** some UPI apps refuse an amount filled in for a personal UPI ID. If that happens,
the QR and copy-ID still work; a free business UPI QR (PhonePe or Paytm for Business) usually
avoids it. Find out before the pilot, with each kitchen's UPI ID.

## During the pilot

- Admin → **Pilot stats** every evening: orders per day, rejected and expired, disputes.
- Ask each kitchen how many orders still came through WhatsApp.
- Feedback calls with owners and a few customers after week 1 and week 2.
- Watch **Admin → Error log** daily.
- Download a backup weekly.

## SMS provider (needed before production sign-in)

Choose an Indian SMS provider with OTP support (for example MSG91, 2Factor or Fast2SMS), register
the sender ID and OTP template on DLT, and share the provider's API details with the developer.
The app needs one new `ISmsSender` class; nothing else changes.
