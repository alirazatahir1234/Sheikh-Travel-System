import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { MatStepperModule } from '@angular/material/stepper';
import { PaymentPlanSummaryComponent } from '../../shared/components/payment-plan-summary/payment-plan-summary.component';
import { BookingStatusBannerComponent } from '../../shared/components/booking-status-banner/booking-status-banner.component';
import { BookingLedgerPaymentSummaryComponent } from '../../shared/components/booking-ledger-payment-summary/booking-ledger-payment-summary.component';
import { BookingListComponent } from './booking-list/booking-list.component';
import { BookingWizardComponent } from './booking-wizard/booking-wizard.component';
import { BookingDetailComponent } from './booking-detail/booking-detail.component';
import { BookingEditComponent } from './booking-edit/booking-edit.component';
import { BookingInvoiceComponent } from './booking-invoice/booking-invoice.component';

const routes: Routes = [
  { path: '',            component: BookingListComponent    },
  { path: 'new',         component: BookingWizardComponent  },
  { path: ':id',         component: BookingDetailComponent  },
  { path: ':id/edit',    component: BookingEditComponent    },
  { path: ':id/invoice', component: BookingInvoiceComponent }
];

@NgModule({
  declarations: [BookingListComponent, BookingWizardComponent, BookingDetailComponent, BookingEditComponent, BookingInvoiceComponent],
  imports: [
    SharedModule,
    MatStepperModule,
    PaymentPlanSummaryComponent,
    BookingStatusBannerComponent,
    BookingLedgerPaymentSummaryComponent,
    RouterModule.forChild(routes)
  ]
})
export class BookingsModule {}
