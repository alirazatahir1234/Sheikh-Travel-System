import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { MatStepperModule } from '@angular/material/stepper';
import { BookingListComponent } from './booking-list/booking-list.component';
import { BookingWizardComponent } from './booking-wizard/booking-wizard.component';
import { BookingDetailComponent } from './booking-detail/booking-detail.component';

const routes: Routes = [
  { path: '', component: BookingListComponent },
  { path: 'new', component: BookingWizardComponent },
  { path: ':id', component: BookingDetailComponent }
];

@NgModule({
  declarations: [BookingListComponent, BookingWizardComponent, BookingDetailComponent],
  imports: [SharedModule, MatStepperModule, RouterModule.forChild(routes)]
})
export class BookingsModule {}
