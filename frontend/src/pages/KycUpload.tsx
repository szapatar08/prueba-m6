import { useState, useEffect, useCallback } from 'react';
import { kycApi } from '../services/api';
import type { KycStatus } from '../types';

export default function KycUpload() {
  const [status, setStatus] = useState<KycStatus | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const fetchStatus = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await kycApi.getStatus();
      setStatus(data);
    } catch {
      // User might not have KYC yet
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;

    setError('');
    setSuccess('');
    setUploading(true);

    try {
      await kycApi.upload(file);
      setSuccess('Document uploaded successfully. It will be processed shortly.');
      setFile(null);
      fetchStatus();
    } catch (err: unknown) {
      const axiosError = err as { response?: { data?: { error?: string } } };
      setError(axiosError.response?.data?.error || 'Upload failed');
    } finally {
      setUploading(false);
    }
  };

  const statusDisplay: Record<string, { label: string; color: string }> = {
    Pending: { label: 'Pending Review', color: 'bg-yellow-100 text-yellow-800' },
    Approved: { label: 'Approved', color: 'bg-green-100 text-green-800' },
    Rejected: { label: 'Rejected', color: 'bg-red-100 text-red-800' },
  };

  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600" />
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Identity Verification (KYC)</h1>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Current Status</h2>
        {status ? (
          <div className="space-y-3">
            <div className="flex items-center space-x-3">
              <span className="text-sm font-medium text-gray-500">Status:</span>
              <span className={`px-3 py-1 rounded-full text-xs font-medium ${statusDisplay[status.status]?.color || 'bg-gray-100 text-gray-800'}`}>
                {statusDisplay[status.status]?.label || status.status}
              </span>
            </div>
            {status.validatedAt && (
              <div className="flex items-center space-x-3">
                <span className="text-sm font-medium text-gray-500">Validated:</span>
                <span className="text-sm text-gray-900">{new Date(status.validatedAt).toLocaleDateString()}</span>
              </div>
            )}
            {status.documents.length > 0 && (
              <div>
                <span className="text-sm font-medium text-gray-500">Documents:</span>
                <ul className="mt-2 space-y-1">
                  {status.documents.map((doc) => (
                    <li key={doc.id} className="text-sm text-gray-600">
                      {doc.fileName} &mdash; uploaded {new Date(doc.uploadedAt).toLocaleDateString()}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        ) : (
          <p className="text-sm text-gray-500">No KYC verification on file. Upload a document to get started.</p>
        )}
      </div>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
        <h2 className="text-lg font-semibold text-gray-900 mb-4">Upload Document</h2>
        <p className="text-sm text-gray-500 mb-4">
          Upload a clear photo of your identity document (passport, national ID, or driver's license).
          Your document will be processed securely and deleted after 90 days.
        </p>

        {error && (
          <div className="rounded-md bg-red-50 p-4 mb-4">
            <p className="text-sm text-red-700">{error}</p>
          </div>
        )}
        {success && (
          <div className="rounded-md bg-green-50 p-4 mb-4">
            <p className="text-sm text-green-700">{success}</p>
          </div>
        )}

        <form onSubmit={handleUpload} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Select document file
            </label>
            <input
              type="file"
              accept="image/*,.pdf"
              onChange={(e) => setFile(e.target.files?.[0] || null)}
              className="block w-full text-sm text-gray-500 file:mr-4 file:py-2 file:px-4 file:rounded-md file:border-0 file:text-sm file:font-medium file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100"
            />
          </div>

          <button
            type="submit"
            disabled={!file || uploading}
            className="w-full py-2 px-4 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {uploading ? 'Uploading...' : 'Upload Document'}
          </button>
        </form>
      </div>
    </div>
  );
}
