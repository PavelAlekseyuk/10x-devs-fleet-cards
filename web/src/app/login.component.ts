import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonComponent } from './shared/ui/button.component';
import { AuthService } from './auth.service';
import { LanguageService } from './language.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, ButtonComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly router = inject(Router);
  protected username = '';
  protected password = '';

  protected async submit(): Promise<void> {
    if (!this.username.trim() || !this.password) {
      this.auth.error.set(this.language.text('genericError'));
      return;
    }

    if (await this.auth.login({ UserName: this.username, Password: this.password })) {
      await this.router.navigateByUrl('/overview');
    }
  }
}