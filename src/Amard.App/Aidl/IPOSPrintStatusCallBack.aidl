// IPOSPrintStatusCallBack.aidl
package com.persianswitch.smartpos.aidl;

interface IPOSPrintStatusCallBack {


       /**
            * This method will call if the printing operation completed successfully.
            * @param printId - the id of the BitmapPrintObject item that its printing completed.
            */
           void onSuccess(int printId);


           /**
            * This method will call when printing operation started successfully.
            * @param printId - the id of the BitmapPrintObject item that its printing completed.
            */
           void onStartPrinting(int printId);


           /**
            * This method will call when some error occurred in the process of printing.
            * @param errorCode - an error code from the enum error codes in this interface that shares some
            *             details about the error.
            * @param printId - Id of print object that faced with error.
            */
           void onError(int errorCode,int printId);

}
