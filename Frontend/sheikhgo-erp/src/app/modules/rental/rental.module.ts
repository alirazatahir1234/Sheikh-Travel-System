import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { UiEmptyStateComponent } from '../../shared/components/ui/empty-state/ui-empty-state.component';
import { RentalListComponent } from './rental-list/rental-list.component';
import { RentalDetailComponent } from './rental-detail/rental-detail.component';
import { RentalRatesComponent } from './rental-rates/rental-rates.component';
import { RentalAvailabilityComponent } from './rental-availability/rental-availability.component';

const routes: Routes = [
  { path: '', component: RentalListComponent },
  { path: 'rates', component: RentalRatesComponent },
  { path: 'availability', component: RentalAvailabilityComponent },
  { path: ':id', component: RentalDetailComponent }
];

@NgModule({
  declarations: [
    RentalListComponent,
    RentalDetailComponent,
    RentalRatesComponent,
    RentalAvailabilityComponent
  ],
  imports: [SharedModule, RouterModule.forChild(routes), UiEmptyStateComponent]
})
export class RentalModule {}
