import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { SettlementListComponent } from './settlement-list/settlement-list.component';
import { SettlementDetailComponent } from './settlement-detail/settlement-detail.component';

const routes: Routes = [
  { path: '', component: SettlementListComponent },
  { path: ':id', component: SettlementDetailComponent }
];

@NgModule({
  declarations: [SettlementListComponent, SettlementDetailComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class SettlementsModule {}
