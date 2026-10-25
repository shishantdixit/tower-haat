import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { OwnerApi } from '../core/api/business.api';
import { errorMessage } from '../core/api/errors';
import { resizeImage } from '../core/media/resize-image';
import { OwnerStore } from './owner-store';

const MAX_PHOTOS = 6;

/** Business photos: the first one is the cover in the society's list. */
@Component({
  selector: 'app-photos',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p><a routerLink="/owner">← My business</a></p>
    <h1>Photos</h1>
    <p class="muted">Your kitchen, your food, your products. The first photo is your cover.</p>

    @if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    }

    <div class="grid">
      @for (photo of photos(); track photo.id; let first = $first) {
        <figure>
          <img [src]="photo.url" alt="" />
          @if (first) {
            <figcaption>Cover</figcaption>
          }
          <button
            type="button"
            class="remove"
            [disabled]="busy()"
            (click)="remove(photo.id)"
            aria-label="Remove photo"
          >
            ×
          </button>
        </figure>
      }
      @if (photos().length < max) {
        <label class="add" [class.busy]="busy()">
          <input type="file" accept="image/*" [disabled]="busy()" (change)="add($event)" />
          <span>{{ busy() ? 'Uploading…' : '+ Add photo' }}</span>
        </label>
      }
    </div>
  `,
  styles: `
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
      gap: 8px;
    }
    figure,
    .add {
      position: relative;
      margin: 0;
      aspect-ratio: 1;
      border-radius: 8px;
      overflow: hidden;
    }
    figure img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    figcaption {
      position: absolute;
      left: 6px;
      bottom: 6px;
      padding: 2px 8px;
      border-radius: 999px;
      background: rgb(0 0 0 / 0.6);
      color: #fff;
      font-size: 0.75rem;
    }
    .remove {
      position: absolute;
      top: 4px;
      right: 4px;
      min-height: 32px;
      width: 32px;
      padding: 0;
      border-radius: 50%;
      background: rgb(255 255 255 / 0.9);
    }
    .add {
      display: flex;
      align-items: center;
      justify-content: center;
      border: 2px dashed var(--th-border);
      color: var(--th-primary);
      font-weight: 600;
      cursor: pointer;
    }
    .add input {
      position: absolute;
      inset: 0;
      opacity: 0;
      cursor: pointer;
    }
  `,
})
export default class Photos implements OnInit {
  private readonly store = inject(OwnerStore);
  private readonly api = inject(OwnerApi);

  protected readonly max = MAX_PHOTOS;
  protected readonly photos = computed(() => this.store.business()?.photos ?? []);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): Promise<unknown> {
    return this.store.load();
  }

  protected async add(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    await this.run(async () => {
      await this.api.addPhoto(await resizeImage(file));
      await this.store.load(true);
    });
  }

  protected remove(photoId: string): Promise<void> {
    return this.run(async () => {
      await this.api.deletePhoto(photoId);
      await this.store.load(true);
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch (error) {
      // Resize problems are plain errors; API failures are HTTP errors.
      this.error.set(error instanceof Error ? error.message : errorMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
