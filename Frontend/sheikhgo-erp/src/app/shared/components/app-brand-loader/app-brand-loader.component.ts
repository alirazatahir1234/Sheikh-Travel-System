import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { APP_PRODUCT_NAME } from '../../../core/constants/app-brand';
import { SgProgressLoaderComponent, SgLoaderSize } from '../sg-progress-loader/sg-progress-loader.component';

export type BrandLoaderSize = SgLoaderSize;

@Component({
  selector: 'app-brand-loader',
  standalone: true,
  imports: [SgProgressLoaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="brand-loader"
      [class.brand-loader--sm]="size() === 'sm'"
      [class.brand-loader--lg]="size() === 'lg'"
      role="status"
      [attr.aria-label]="ariaLabel()"
    >
      <sg-progress-loader [size]="size()" />
      @if (message()) {
        <p class="brand-loader__message">{{ message() }}</p>
      }
    </div>
  `,
  styles: [`
    .brand-loader {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 14px;
      padding: 48px 24px;
      width: 100%;
      box-sizing: border-box;
    }

    .brand-loader--sm {
      padding: 28px 16px;
      gap: 10px;
    }

    .brand-loader--lg {
      padding: 64px 24px;
      gap: 16px;
    }

    .brand-loader__message {
      margin: 0;
      font-family: Inter, system-ui, sans-serif;
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--stb-text-muted, #64748b);
      text-align: center;
    }
  `]
})
export class AppBrandLoaderComponent {
  readonly size = input<BrandLoaderSize>('md');
  readonly message = input<string | undefined>(undefined);

  readonly ariaLabel = computed(() => {
    const text = this.message();
    return text ? text : `Loading ${APP_PRODUCT_NAME}`;
  });
}
