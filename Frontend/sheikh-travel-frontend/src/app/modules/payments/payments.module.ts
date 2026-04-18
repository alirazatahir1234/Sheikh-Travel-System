import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { PaymentListComponent } from './payment-list/payment-list.component';
import { PaymentFormComponent } from './payment-form/payment-form.component';

const routes: Routes = [
  { path: '', component: PaymentListComponent },
  { path: 'new', component: PaymentFormComponent }
];

@NgModule({
  declarations: [PaymentListComponent, PaymentFormComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class PaymentsModule {}
