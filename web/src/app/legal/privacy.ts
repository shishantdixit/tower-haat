import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LEGAL } from './legal';

/** Privacy notice under India's Digital Personal Data Protection Act, 2023. */
@Component({
  selector: 'app-privacy',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="legal">
      <h1>Privacy notice</h1>
      <p class="muted">Last updated {{ legal.lastUpdated }}</p>

      <p>
        TowerHaat helps residents of an apartment society find and order from home businesses in the
        same society. This notice explains what personal data we use, why, and your rights.
        {{ legal.operator }} is responsible for your data.
      </p>

      <h2>What we collect</h2>
      <ul>
        <li><strong>Your phone number</strong>, to sign you in with a one-time code.</li>
        <li><strong>Your name, society, tower and flat</strong>, to deliver your orders.</li>
        <li>
          <strong>Your orders</strong>: items, amounts, notes, delivery times, payment method and
          payment status, including a UPI reference number if you choose to give one.
        </li>
        <li><strong>Notification settings</strong>, if you turn on notifications on a device.</li>
        <li>
          If you run a business: its details, photos, menu and UPI ID, which residents can see.
        </li>
      </ul>

      <h2>Why we use it</h2>
      <p>
        Only to run TowerHaat: signing you in, showing you your society's businesses, placing and
        tracking orders, letting the business you order from contact you about that order, and
        sending the notifications you turned on. We do not sell your data or show advertising.
      </p>

      <h2>Who can see it</h2>
      <ul>
        <li>
          <strong>A business you order from</strong> sees your name, phone, tower and flat on your
          orders with that business, and nothing else about you.
        </li>
        <li><strong>Other residents</strong> see nothing about you as a customer.</li>
        <li>
          <strong>Residents of your society</strong> see a business's name, photos, menu, hours,
          WhatsApp number and UPI ID, if you run one.
        </li>
        <li>
          <strong>The TowerHaat team</strong> sees accounts, businesses and orders when needed to
          run the service.
        </li>
        <li>
          <strong>Service providers</strong> who process data for us: our hosting provider, the SMS
          provider that sends sign-in codes, and the push services of Google, Apple or Mozilla for
          notifications (sent encrypted, with no phone numbers).
        </li>
      </ul>

      <h2>Payments</h2>
      <p>
        TowerHaat never receives your money or your bank or card details. You pay the business
        directly in your own UPI app, or in cash.
      </p>

      <h2>How long we keep it</h2>
      <p>
        Your account data stays while you have an account. Orders are kept as a record for you and
        the business; after you delete your account they keep the name, phone and flat written on
        them at the time, but are no longer linked to a profile. Sign-in codes expire after five
        minutes, and server logs are deleted after two months.
      </p>

      <h2>Your rights</h2>
      <ul>
        <li>See and correct your details on <a routerLink="/profile">My profile</a>.</li>
        <li>
          Withdraw your consent and erase your data by deleting your account on
          <a routerLink="/profile">My profile</a>.
        </li>
        <li>
          Ask a question or raise a grievance with {{ legal.grievanceOfficer }} at
          <a [href]="'mailto:' + legal.contactEmail">{{ legal.contactEmail }}</a
          >. We reply within 7 days. If you are not satisfied, you may complain to the Data
          Protection Board of India.
        </li>
      </ul>

      <h2>Children</h2>
      <p>
        TowerHaat is for adults. Businesses offering tuition or daycare do not collect data about
        children through TowerHaat.
      </p>

      <p class="muted">See also the <a routerLink="/terms">terms of use</a>.</p>
    </article>
  `,
  styleUrl: './legal.scss',
})
export default class Privacy {
  protected readonly legal = LEGAL;
}
