import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { PaymentListComponent } from './payment-list/payment-list.component';
import { PaymentFormComponent } from './payment-form/payment-form.component';
import { PaymentReceiptComponent } from './payment-receipt/payment-receipt.component';

const routes: Routes = [
  { path: '',                component: PaymentListComponent   },
  { path: 'new',             component: PaymentFormComponent   },
  { path: ':id/receipt',     component: PaymentReceiptComponent }
];

@NgModule({
  declarations: [PaymentListComponent, PaymentFormComponent, PaymentReceiptComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class PaymentsModule {}
