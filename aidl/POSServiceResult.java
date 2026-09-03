package com.persianswitch.smartpos.aidl;

public class POSServiceResult {

  public static class ResultKeys {

    public static final String STATUS_MESSAGE = "status_message";

    public static final String STATUS_CODE = "status_code";

    public static final String RESPONSE_DATA = "response_data";

    public static final String RESPONSE_SIGN = "response_sign";

    public static final String MASKED_CARD = "masked_card";

    public static final String STAN = "stan";

    public static final String TRANSACTION_DATE = "transaction_date";

    public static final String IS_PAYMENT_BY_CREDIT = "is_payment_by_credit";

  }

  public static class ResultCodes {

    public static final int TRANSACTION_COMPLETED = 0;

    public static final int TRANSACTION_UNKNOWN_RESULT = 9999;

    public static final int TRANSACTION_CANCELED_BY_USER = 10001;
    public static final int CARD_SWIPE_TIMED_OUT = 10002;
    public static final int PIN_ENTRY_TIMED_OUT = 10003;
    public static final int INVALID_DATA = 10004;


    public static final int GENERAL_ERROR = 11000;
    public static final int POS_IS_NOT_READY_ERROR = 11001;
    public static final int TRANSACTION_ERROR = 11002;
    public static final int WIPE_REQUIRED = 11003;

  }


  public static class InquiryTransactionStatus
  {
    public static final int SUCCESS = 0;
    public static final int FAILED = 1;
    public static final int UNKNOWN = 2;
  }

  public static class PrintStatusCodes
  {
    public static final int UNKNOWN_ERROR = -1;
    public static final int PRINTER_PAPER_END = 101;
    public static final int PRINTER_PAPER_GENERAL_ERROR = 102;
    public static final int PRINTER_GENERAL_ERROR = 103;
    public static final int PRINTER_BUSY = 104;
    public static final int LOW_BATTERY_ERROR = 105;
  }

}
