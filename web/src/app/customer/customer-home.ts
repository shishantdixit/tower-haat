import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-customer-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Namaste, {{ firstName() }}</h1>
    <p class="muted">
      {{ profile()?.societyName }} · Tower {{ profile()?.tower }}, Flat {{ profile()?.flat }} ·
      <a routerLink="/profile">Edit</a>
    </p>
    <div class="card">
      <p>Kitchens, sellers, tuition and services from your society will appear here soon.</p>
    </div>
  `,
})
export default class CustomerHome {
  private readonly auth = inject(AuthService);

  protected readonly profile = computed(() => this.auth.user()?.profile ?? null);
  protected readonly firstName = computed(() => this.auth.user()?.name?.split(' ')[0] ?? '');
}
