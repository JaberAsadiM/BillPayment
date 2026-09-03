package com.persianswitch.smartpos.aidl.exception;

public enum SmartPOSWebServiceErrorCode {

  GENERAL_WEBSERVICE_FAILURE_ERROR(5000),
  NO_NETWORK_ERROR(5001),
  CONNECTION_TIMEOUT_ERROR(5002),
  SSL_CONFIGURATION_ERROR(5003),
  HTTP_BAD_REQUEST_ERROR(5006),
  UNKNOWN_FAILURE_ERROR(5007);

  private final int code;
  SmartPOSWebServiceErrorCode(int code) {
    this.code = code;
  }
  public int getCode() {
    return code;
  }
}
