import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** The Indian veg / non-veg symbol: a green or brown dot in a square. */
@Component({
  selector: 'app-veg-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.veg]': 'isVeg() === true',
    '[class.nonveg]': 'isVeg() === false',
    '[attr.role]': '"img"',
    '[attr.aria-label]': 'isVeg() ? "Veg" : "Non-veg"',
    '[hidden]': 'isVeg() === null',
  },
  template: '<span></span>',
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 14px;
      height: 14px;
      border: 1.5px solid currentColor;
      border-radius: 2px;
      flex-shrink: 0;
    }
    :host(.veg) {
      color: #15803d;
    }
    :host(.nonveg) {
      color: #9a3412;
    }
    span {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }
  `,
})
export class VegMark {
  readonly isVeg = input<boolean | null>(null);
}
