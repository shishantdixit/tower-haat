import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LEGAL } from './legal';

@Component({
  selector: 'app-terms',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="legal">
      <h1>Terms of use</h1>
      <p class="muted">Last updated {{ legal.lastUpdated }}</p>

      <h2>What TowerHaat is</h2>
      <p>
        TowerHaat is a technology platform, run by {{ legal.operator }}, that lets residents of a
        society find home businesses in the same society and order from them. TowerHaat does not
        make, sell or deliver anything itself.
      </p>

      <h2>Businesses are responsible for what they sell</h2>
      <p>
        Each business is responsible for its food, products and services: quality, safety, hygiene,
        FSSAI registration where required, prices, delivery and refunds. TowerHaat approves
        businesses before they appear but does not inspect them.
      </p>

      <h2>Orders</h2>
      <ul>
        <li>An order is a request to the business. The business may accept or reject it.</li>
        <li>An order not accepted within 30 minutes is cancelled automatically.</li>
        <li>
          You can cancel an order until the business accepts it. After that, contact the business.
        </li>
        <li>A business may cancel an accepted order with a reason, which you will see.</li>
      </ul>

      <h2>Payments</h2>
      <p>
        You pay the business directly, by UPI in your own UPI app or in cash. TowerHaat never
        receives or holds your money and only records the payment status that you and the business
        report. Any refund is settled directly between you and the business.
      </p>

      <h2>Your account</h2>
      <ul>
        <li>One account per phone number. Keep sign-in codes to yourself.</li>
        <li>Give your real name and flat, so orders reach you.</li>
        <li>
          Do not place false orders, harass businesses or residents, or misuse the service. We may
          suspend accounts or businesses that do.
        </li>
        <li>You can delete your account at any time on <a routerLink="/profile">My profile</a>.</li>
      </ul>

      <h2>Liability</h2>
      <p>
        TowerHaat is provided as it is. To the extent the law allows, {{ legal.operator }} is not
        liable for the goods or services of businesses, for disputes between customers and
        businesses, or for losses from the service being unavailable.
      </p>

      <h2>Changes and contact</h2>
      <p>
        We may update these terms and will show the new date here. Questions:
        <a [href]="'mailto:' + legal.contactEmail">{{ legal.contactEmail }}</a
        >. These terms are governed by the laws of India, and the courts of {{ legal.city }} have
        jurisdiction.
      </p>

      <p class="muted">See also the <a routerLink="/privacy">privacy notice</a>.</p>
    </article>
  `,
  styleUrl: './legal.scss',
})
export default class Terms {
  protected readonly legal = LEGAL;
}
