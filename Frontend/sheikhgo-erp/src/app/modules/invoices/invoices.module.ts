import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SharedModule } from '../../shared/shared.module';
import { InvoiceListComponent } from './invoice-list/invoice-list.component';
import { InvoiceDetailComponent } from './invoice-detail/invoice-detail.component';

const routes: Routes = [
  { path: '', component: InvoiceListComponent },
  { path: ':id', component: InvoiceDetailComponent }
];

@NgModule({
  declarations: [InvoiceListComponent, InvoiceDetailComponent],
  imports: [SharedModule, RouterModule.forChild(routes)]
})
export class InvoicesModule {}
