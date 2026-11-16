import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'CGP - Randevu Takip',
  description: 'Kurum içi randevu ve görüşme takibi',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="tr">
      <body>{children}</body>
    </html>
  );
}
