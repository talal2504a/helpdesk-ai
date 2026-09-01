import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext.jsx';
import Layout from './components/Layout.jsx';
import Login from './pages/Login.jsx';
import Register from './pages/Register.jsx';
import MyTickets from './pages/MyTickets.jsx';
import TicketDetail from './pages/TicketDetail.jsx';
import CreateTicket from './pages/CreateTicket.jsx';
import AgentQueue from './pages/AgentQueue.jsx';
import Dashboard from './pages/Dashboard.jsx';
import AdminUsers from './pages/AdminUsers.jsx';
import AdminLookups from './pages/AdminLookups.jsx';

function Protected({ roles = null, children }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(user.role)) return <Navigate to="/" replace />;
  return children;
}

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/" element={<Protected><MyTickets /></Protected>} />
        <Route path="/tickets" element={<Protected><MyTickets /></Protected>} />
        <Route path="/tickets/new" element={<Protected roles={["Customer"]}><CreateTicket /></Protected>} />
        <Route path="/tickets/:id" element={<Protected><TicketDetail /></Protected>} />
        <Route path="/queue" element={<Protected roles={["Agent","Admin"]}><AgentQueue /></Protected>} />
        <Route path="/dashboard" element={<Protected roles={["Agent","Admin"]}><Dashboard /></Protected>} />
        <Route path="/admin/users" element={<Protected roles={["Admin"]}><AdminUsers /></Protected>} />
        <Route path="/admin/lookups" element={<Protected roles={["Admin"]}><AdminLookups /></Protected>} />
      </Routes>
    </Layout>
  );
}
