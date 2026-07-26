import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import App from './App.tsx';
import { CompsPage } from './CompsPage.tsx';
import { HistoryPage } from './HistoryPage.tsx';
import { LiveCoachPage } from './LiveCoachPage.tsx';
import { Layout } from './Layout.tsx';
import { PatchNotesPage } from './PatchNotesPage.tsx';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<App />} />
          <Route path="history" element={<HistoryPage />} />
          <Route path="comps" element={<CompsPage />} />
          <Route path="live-coach" element={<LiveCoachPage />} />
          <Route path="patch-notes" element={<PatchNotesPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  </StrictMode>,
);
