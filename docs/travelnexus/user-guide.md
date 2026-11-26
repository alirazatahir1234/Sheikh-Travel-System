# SheikhGo TravelNexus — user guide

For travel agency staff: counter agents, visa officers, group coordinators and accounts. TravelNexus lives under
**Travel** in the SheikhGo menu. What you see depends on the permissions your administrator gave you; a page or button
you cannot find is one you do not have access to.

The short version of every workflow is also in the app: click **?** (Help) and choose **Travel**.

## 1. First-time setup (administrator)

Travel → **Settings**

- **General**: default currency, reporting currency (what margins and the profit report are shown in), number prefixes
  (enquiries, visa cases, invoices, receipts, groups, bookings…), passport-expiry warning days, reminder quiet hours.
- **Visa types**: one per country and category (Umrah, Tourist, Business…) with entry type, validity, processing days
  and the default embassy fee.
- **Document types**: passport copy, photo, CNIC, bank statement… and whether each one carries an expiry date.
- **Checklists**: per country and visa type, the documents an applicant must provide (mandatory or optional, who it
  applies to: adults, minors, women travelling with a mahram). Activate a checklist to use it; a new version never
  changes open cases.
- **Lead sources** and **lost reasons** for enquiries.
- **Hotels** (Travel → Packages → Hotels) and **Suppliers** (airlines, hotels, bed banks, transport) with their
  currency, rate cards and supplier commission rules.
- **Exchange rates** (Travel → Finance → Rates): one row per currency with the date it applies from, used to bring
  foreign-currency sales and costs into the reporting currency.
- **Commission rules** (Travel → Commissions): what staff agents and sub-agents earn, as a percentage of margin or of
  sell, for all services or one kind.

## 2. Customers and travelers

Travel → **Customers**

- A customer is the person or company you invoice. Add them once; link travelers (the people who actually travel)
  under the customer. A traveler's passport number and CNIC are masked unless you hold the view-PII permission.
- **Passport scan**: on a traveler, upload a photo of the passport page to pre-fill the names, dates and number
  (nothing is stored from the scan itself). Check every field before saving.
- The customer page shows their enquiries, visa cases, groups, bookings, invoices and balance.

## 3. Enquiries

Travel → **Enquiries**

1. **New enquiry**: lead name and phone (or an existing customer), service (visa, Umrah, flight, hotel, package),
   destination, dates, number of travelers, budget, assigned agent, next follow-up.
2. Log every call, message or meeting as an **activity** and set the next follow-up; the dashboard and the
   follow-up reminder work from that date.
3. Move the enquiry along: **Contacted → Qualified → Quoted**. Mark it **Lost** with a reason, or **Convert** it.
4. **Convert** creates the customer (if new) and, if you tick it, opens a Draft visa case with the chosen visa type.

## 4. Visa cases

Travel → **Visa cases**

1. A case belongs to one customer (or to an Umrah group) and one visa type. Add the **applicants** (the customer's
   travelers). If a checklist is active for that country and visa type, each applicant's required documents appear.
2. **Start documents**: upload each document (PDF, JPG or PNG, up to 10 MB). Verify it, or reject it with a reason so
   the customer sends it again, or waive it when it is not needed. You can also request an extra document.
3. **Verify documents** closes the gate: the case can only be submitted once every mandatory document is verified or
   waived.
4. **Submit** with the submission date and reference; **Start processing** when the embassy has it; set the
   **appointment** and **expected decision** dates so reminders can work.
5. **Record decisions** per applicant: approved (visa number and validity), rejected (reason), or withdrawn. The case
   becomes Approved, Partially approved or Rejected once every applicant is decided.
6. **Hold** and **Resume** pause a case; **Cancel** closes it with a reason.

Travel → Visa cases → **Missing documents** lists every open case that still lacks a mandatory document.

## 5. Hotels, packages and Umrah groups

Travel → **Packages**

- A package is an Umrah or Hajj programme: nights in Makkah and Madinah, hotel stays, visa type, prices per room type
  (quad, triple, double, single) for adults, children and infants, in one currency. **Publish** it before it can be used.

Travel → **Groups**

1. **New group** from a published package: name, departure date, capacity. Add the hotel **stays** (check-in and
   check-out per hotel) and **transport legs** (airport transfers, intercity, Ziyarat).
2. **Open registration**, then **Register pilgrims**: pick the customer and their travelers, the room preference and
   price category; the quoted price comes from the package. **Confirm** each pilgrim once they are committed.
3. **Rooms**: assign confirmed pilgrims to rooms per stay. "Suggest" fills rooms by gender policy (male, female,
   family) and room size; adjust and save. Nobody is left without a bed unnoticed: the readiness panel shows it.
4. **Close registration**, then **Start visa processing**: a group visa case opens with every confirmed pilgrim as an
   applicant. Work it like any visa case.
5. **Cancelling a pilgrim** frees their bed and takes them off the group visa case in the same step (removed before
   submission, withdrawn after). If the pilgrim already holds a ticket, the message tells you to cancel it in the
   group's booking — tickets are never cancelled silently because penalties and refunds must be recorded.
6. **Mark ready** when every pilgrim is confirmed, has a visa, a bed and a ticket; **Depart**, **Return**, **Close**.
7. **Manifest**: the departure list with passport details (needs the manifest permission; every view is audited).
   Export it as PDF or Excel.

## 6. Bookings (flights and hotels)

Travel → **Bookings**

1. A **booking file** belongs to a customer or to a group. Add **flights** (supplier, direction, segments with airports
   and local times, passengers, sell and cost amounts in the service currency) and **hotel bookings** (hotel, dates,
   meal plan, rooms, guests, sell and cost).
2. Flights move **Requested → On hold (PNR + ticketing time limit) → Confirmed → Ticketed** (enter each passenger's
   ticket number). Hotels move **Requested → Confirmed (confirmation number) → Checked in → Checked out**.
3. **Cancel** a service or a single passenger with the supplier penalty, the customer penalty and the expected refund;
   then track the refund status until it is paid to the customer. Vouchers and itineraries are printed from the file.

## 7. Finance

Travel → **Finance**

- **Invoices**: pick the customer and the billable items (confirmed flights, hotel bookings, visa fees, group
  places); each sold item is invoiced once. Add installments if the customer pays in parts. **Issue** the draft to
  make it final; **Void** with a reason if it was wrong (credit notes adjust an issued invoice).
- **Receipts**: record what the customer paid (cash, bank transfer, card, cheque, online), allocate it to invoices,
  mark cheques and transfers as cleared or failed, and reconcile with the bank reference. Recording the same receipt
  twice (same request key) does not create a duplicate.
- **Balances and statements**: per customer and currency, with every invoice, receipt, credit note and refund.
- **Payables** (Travel → Finance → Payables): enter **supplier bills** against the services they cover (the supplier's
  own commission is netted on the bill), **post** them, record **supplier payments**, credits and refunds received, and
  see each supplier's balance and statement.

Travel → **Commissions**

- Staff agents and sub-agents earn commission on issued invoices under the rules you set. Commission is **accrued**
  while the invoice is owed and **payable** once it is paid. Record **payouts** against the payable balance; the system
  never lets a payout exceed it, even when two people record payouts at the same moment.

Travel → **Profitability** (restricted)

- Margin per booking, visa case and group: revenue from invoices, cost from supplier bills (and recorded costs not yet
  billed), commission, margin, all in the reporting currency. "Missing rate" tells you an exchange rate is needed.

## 8. Reminders

Travel → **Reminders**

- **Rules**: for each kind (passport expiry, visa appointment, missing documents, visa decision overdue, visa status
  changed, ticketing deadline, flight departure, hotel check-in, invoice due, invoice overdue, enquiry follow-up,
  group departure) choose the lead times, who gets it (staff in-app or by email, customers by email, SMS or WhatsApp)
  and the customer text. Placeholders: `{{Name}}`, `{{Reference}}`, `{{Date}}`, `{{Amount}}`.
- WhatsApp to a customer outside the 24-hour messaging window needs your approved WhatsApp template; otherwise the
  log shows the reminder as Failed with the provider's reason.
- **Log**: every reminder sent, skipped (with the reason, e.g. no assigned agent or no phone number) or failed.
  **Run now** runs the rules immediately for your company. Nothing is ever sent twice for the same due date.
- Quiet hours (Settings) hold customer messages until the morning.

## 9. Dashboard and reports

Travel → **Dashboard** (the Travel landing page)

- Pipelines (enquiries, visa cases, groups, bookings by status), "needs attention" counts that link to the filtered
  pages, receivables and payables per currency, trends, and the next 14 days of deadlines. Every card says which
  window it covers.

Travel → **Reports & Analytics**

- Twelve reports: enquiry performance, visa processing, country applications, pending documents, group manifests,
  bookings, agent and branch performance, customer balances, supplier balances, pilgrim payments, profit and loss,
  commissions. Choose the date range (and branch, agent, country or dimension where offered), then export to PDF,
  Excel or CSV. Money is always shown with its currency code; totals are only shown when every row is in one currency.

## 10. Good habits

- Set the **assigned agent** on customers, enquiries, cases and files: staff reminders go to them and commission is
  attributed to them.
- Keep **passport expiry dates** filled in: the expiry reminder and the dashboard warning depend on them.
- Record money the day it moves, with the bank reference, so statements and commission balances stay right.
- Never share a document link: files open only inside SheikhGo for people with the download permission, and every
  open is logged.
